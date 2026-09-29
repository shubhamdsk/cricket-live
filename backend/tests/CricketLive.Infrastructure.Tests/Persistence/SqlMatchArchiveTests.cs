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

        var stored = await archive.GetFinishedAsync(0, 20, default);

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

        Assert.Equal(1, await archive.CountFinishedAsync(default));
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

        var stored = await archive.GetFinishedAsync(0, 20, default);

        Assert.Equal(["third", "second", "first"], stored.Select(match => match.Id));
    }

    [Fact]
    public async Task Paging_walks_the_whole_archive_without_repeating_a_match()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [.. Enumerable.Range(1, 5).Select(day => Match($"m{day}", MatchStatus.Completed, Day(day)))],
            default);

        var first = await archive.GetFinishedAsync(0, 2, default);
        var second = await archive.GetFinishedAsync(2, 2, default);
        var third = await archive.GetFinishedAsync(4, 2, default);

        Assert.Equal(["m5", "m4"], first.Select(match => match.Id));
        Assert.Equal(["m3", "m2"], second.Select(match => match.Id));
        Assert.Equal(["m1"], third.Select(match => match.Id));
        Assert.Equal(5, await archive.CountFinishedAsync(default));
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

        var stored = await archive.GetFinishedAsync(0, 20, default);

        Assert.Equal(["fine"], stored.Select(match => match.Id));
    }

    public void Dispose()
    {
        database.Dispose();
        connection.Dispose();
    }

    private SqlMatchArchive Build()
        => new(database, clock, NullLogger<SqlMatchArchive>.Instance);

    private static string Json(MatchDetailsDto? match)
        => JsonSerializer.Serialize(match, JsonSerializerOptions.Web);

    private static DateTimeOffset Day(int day) => new(2026, 1, day, 9, 0, 0, TimeSpan.Zero);

    private static MatchDetailsDto Match(string id, MatchStatus status)
        => Match(id, status, Day(1));

    private static MatchDetailsDto Match(string id, MatchStatus status, DateTimeOffset start)
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

        return new MatchDetailsDto
        {
            Id = id,
            Slug = $"india-vs-australia-{id}",
            Status = status,
            Format = MatchFormat.Odi,
            SeriesName = "A series",
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
