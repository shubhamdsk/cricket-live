using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CricketLive.Infrastructure.Tests.Persistence;

/// <summary>
/// Runs against a real SQLite database held in memory, so the schema, the indexes and the
/// round trip through JSON are all exercised as they are in production.
/// </summary>
public sealed class SqlMatchArchiveTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CricketLiveDbContext database;
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

    public SqlMatchArchiveTests()
    {
        connection.Open();

        database = new CricketLiveDbContext(
            new DbContextOptionsBuilder<CricketLiveDbContext>().UseSqlite(connection).Options);

        database.Database.EnsureCreated();
    }

    [Fact]
    public async Task Only_finished_matches_are_kept()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [Match("done", MatchStatus.Completed), Match("playing", MatchStatus.Live)],
            default);

        var stored = await archive.GetFinishedAsync(MatchFilter.None, 0, 20, default);

        Assert.Equal(["done"], stored.Select(match => match.Id));
    }

    [Fact]
    public async Task A_match_reads_back_exactly_as_it_was_written()
    {
        var archive = Build();
        var original = Match("a", MatchStatus.Completed) with
        {
            StatusText = "India won by 8 wkts",
            Home = new TeamInningsDto(
                new TeamDto("india", "India", "IND", null),
                [new InningsScoreDto(1, 287, 6, "50.0")]),
        };

        await archive.SaveFinishedAsync([original], default);

        var stored = await archive.GetAsync("a", default);

        // Compared as JSON rather than with record equality, which sees two lists holding the same
        // innings as different because it compares the lists by reference.
        Assert.Equal(Json(original), Json(stored));
    }

    [Fact]
    public async Task Saving_the_same_match_twice_keeps_the_first_copy()
    {
        var archive = Build();
        await archive.SaveFinishedAsync([Match("a", MatchStatus.Completed)], default);

        // A finished match should not change. If it somehow does, we keep what we recorded at the
        // time rather than letting a later provider correction quietly rewrite history.
        var corrected = Match("a", MatchStatus.Completed) with { StatusText = "Something else" };
        await archive.SaveFinishedAsync([corrected], default);

        Assert.Equal(1, await archive.CountFinishedAsync(MatchFilter.None, default));
        Assert.Equal(string.Empty, (await archive.GetAsync("a", default))?.StatusText);
    }

    [Fact]
    public async Task Matches_come_back_most_recently_played_first()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("first", MatchStatus.Completed, Day(1)),
                Match("third", MatchStatus.Completed, Day(3)),
                Match("second", MatchStatus.Completed, Day(2)),
            ],
            default);

        var stored = await archive.GetFinishedAsync(MatchFilter.None, 0, 20, default);

        Assert.Equal(["third", "second", "first"], stored.Select(match => match.Id));
    }

    [Fact]
    public async Task Paging_walks_the_whole_archive_without_repeating_a_match()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [.. Enumerable.Range(1, 5).Select(day => Match($"m{day}", MatchStatus.Completed, Day(day)))],
            default);

        var first = await archive.GetFinishedAsync(MatchFilter.None, 0, 2, default);
        var second = await archive.GetFinishedAsync(MatchFilter.None, 2, 2, default);
        var third = await archive.GetFinishedAsync(MatchFilter.None, 4, 2, default);

        Assert.Equal(["m5", "m4"], first.Select(match => match.Id));
        Assert.Equal(["m3", "m2"], second.Select(match => match.Id));
        Assert.Equal(["m1"], third.Select(match => match.Id));
        Assert.Equal(5, await archive.CountFinishedAsync(MatchFilter.None, default));
    }

    [Fact]
    public async Task A_match_is_findable_by_its_slug_as_well_as_its_id()
    {
        var archive = Build();
        await archive.SaveFinishedAsync([Match("abc123", MatchStatus.Completed)], default);

        Assert.NotNull(await archive.GetAsync("india-vs-australia-abc123", default));
    }

    [Fact]
    public async Task A_payload_that_can_no_longer_be_read_is_skipped_rather_than_thrown()
    {
        // Stands in for the DTO changing shape under rows written by an older build. One unreadable
        // match must not take the results page down with it.
        database.ArchivedMatches.Add(new ArchivedMatch
        {
            Id = "broken",
            Slug = "broken",
            StartTimeUtc = Day(9).UtcDateTime,
            SeriesName = "A series",
            Payload = "{ this is not json",
            ArchivedAtUtc = clock.GetUtcNow().UtcDateTime,
        });

        await database.SaveChangesAsync();

        var archive = Build();
        await archive.SaveFinishedAsync([Match("fine", MatchStatus.Completed, Day(1))], default);

        var stored = await archive.GetFinishedAsync(MatchFilter.None, 0, 20, default);

        Assert.Equal(["fine"], stored.Select(match => match.Id));
    }

    [Fact]
    public async Task A_series_filter_is_matched_whole_and_case_insensitively()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("a", MatchStatus.Completed, Day(1), "West Indies tour of India, 2026"),
                Match("b", MatchStatus.Completed, Day(2), "Australia A tour of India, 2026"),
            ],
            default);

        var exact = await Ids(archive, new MatchFilter(SeriesName: "West Indies tour of India, 2026"));
        var lowered = await Ids(archive, new MatchFilter(SeriesName: "west indies tour of india, 2026"));
        var partial = await Ids(archive, new MatchFilter(SeriesName: "tour of India, 2026"));

        Assert.Equal(["a"], exact);
        // The collation, not the comparison, is what makes this work in SQL.
        Assert.Equal(["a"], lowered);
        // A substring would have matched both series, which is a filter quietly becoming a search.
        Assert.Empty(partial);
    }

    [Fact]
    public async Task A_series_containing_a_wildcard_character_is_not_a_wildcard()
    {
        // The reason this is equality and not LIKE. A real series name is unlikely to contain %,
        // but a filter that treats one as "match anything" is wrong in a way nobody would guess.
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("a", MatchStatus.Completed, Day(1), "100% Cricket League"),
                Match("b", MatchStatus.Completed, Day(2), "Another series"),
            ],
            default);

        Assert.Equal(["a"], await Ids(archive, new MatchFilter(SeriesName: "100% Cricket League")));
        Assert.Empty(await Ids(archive, new MatchFilter(SeriesName: "%")));
    }

    [Fact]
    public async Task A_date_range_is_inclusive_at_the_start_and_exclusive_at_the_end()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [.. Enumerable.Range(1, 4).Select(day => Match($"m{day}", MatchStatus.Completed, Day(day)))],
            default);

        // Day(2) starts at 09:00, so a range from Day(2) to Day(4) holds m2 and m3 and not m4.
        var window = await Ids(archive, new MatchFilter(FromUtc: Day(2), ToUtc: Day(4)));

        Assert.Equal(["m3", "m2"], window);
    }

    [Fact]
    public async Task The_count_agrees_with_the_filtered_page()
    {
        // The pair that matters most: a count taken over everything while the page is filtered
        // makes "load more" offer a page that does not exist.
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("a", MatchStatus.Completed, Day(1), "Kept"),
                Match("b", MatchStatus.Completed, Day(2), "Kept"),
                Match("c", MatchStatus.Completed, Day(3), "Other"),
            ],
            default);

        var filter = new MatchFilter(SeriesName: "Kept");

        Assert.Equal(["b", "a"], await Ids(archive, filter));
        Assert.Equal(2, await archive.CountFinishedAsync(filter, default));
    }

    [Fact]
    public async Task Asking_the_archive_for_live_matches_answers_empty()
    {
        // A coherent question with an empty answer, not a mistake: the archive only holds
        // finished matches. Ignoring the clause and returning completed ones would be a lie.
        var archive = Build();
        await archive.SaveFinishedAsync([Match("a", MatchStatus.Completed)], default);

        var filter = new MatchFilter(Status: MatchStatus.Live);

        Assert.Empty(await Ids(archive, filter));
        Assert.Equal(0, await archive.CountFinishedAsync(filter, default));
    }

    [Fact]
    public async Task Series_names_come_from_the_rows_and_are_distinct()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("a", MatchStatus.Completed, Day(1), "Zimbabwe tri-series"),
                Match("b", MatchStatus.Completed, Day(2), "Ashes"),
                Match("c", MatchStatus.Completed, Day(3), "Ashes"),
            ],
            default);

        Assert.Equal(["Ashes", "Zimbabwe tri-series"], await archive.GetSeriesNamesAsync(default));
    }

    public void Dispose()
    {
        database.Dispose();
        connection.Dispose();
    }

    private static async Task<string[]> Ids(SqlMatchArchive archive, MatchFilter filter)
    {
        var page = await archive.GetFinishedAsync(filter, 0, 50, default);
        return [.. page.Select(match => match.Id)];
    }

    private SqlMatchArchive Build()
        => new(database, clock, NullLogger<SqlMatchArchive>.Instance);

    private static string Json(MatchDetailsDto? match)
        => JsonSerializer.Serialize(match, JsonSerializerOptions.Web);

    private static DateTimeOffset Day(int day) => new(2026, 1, day, 9, 0, 0, TimeSpan.Zero);

    private static MatchDetailsDto Match(string id, MatchStatus status)
        => Match(id, status, Day(1));

    private static MatchDetailsDto Match(
        string id,
        MatchStatus status,
        DateTimeOffset start,
        string series = "A series")
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

        return new MatchDetailsDto
        {
            Id = id,
            Slug = $"india-vs-australia-{id}",
            Status = status,
            Format = MatchFormat.Odi,
            SeriesName = series,
            MatchTitle = "1st ODI",
            Venue = "Somewhere",
            StartTimeUtc = start,
            Home = team,
            Away = team,
            StatusText = string.Empty,
            HasBallByBall = false,
            HasSquads = false,
        };
    }
}
