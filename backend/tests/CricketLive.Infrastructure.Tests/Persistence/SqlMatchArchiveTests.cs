using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;
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
            SeriesId = "series-a",
            SeriesName = "A series",
            HomeTeamId = "india",
            AwayTeamId = "australia",
            HomeTeamName = "India",
            AwayTeamName = "Australia",
            // What the writer would have computed for these two sides, so the row is readable and
            // the test is still about an unreadable payload rather than about scope.
            InScope = true,
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
    public async Task A_match_archived_before_a_field_existed_still_reads_back()
    {
        // This is not hypothetical: adding SeriesId as a `required` member made every row already
        // in the archive fail to deserialise, and the results page quietly lost its history. The
        // payload is the durable format, so a new field has to be optional with a default.
        var archive = Build();

        database.ArchivedMatches.Add(Archived("older", Without("seriesId", "older")));

        await database.SaveChangesAsync();

        var page = await archive.GetFinishedAsync(MatchFilter.None, 0, 20, default);

        var match = Assert.Single(page);
        Assert.Equal("older", match.Id);
        Assert.Equal(string.Empty, match.SeriesId);
    }

    [Fact]
    public async Task Any_optional_field_may_be_missing_from_an_archived_payload()
    {
        // The test above is the incident; this is the rule it taught us, applied to every optional
        // field rather than only the one that caught us out. A field the archive can read only when
        // present orphans every row written before it existed, so each is stripped in turn. This
        // covers fields added after today without anyone remembering to come back here.
        var archive = Build();
        var optional = OptionalNames();

        // Without this, deleting the last optional field would leave a test that proves nothing.
        Assert.NotEmpty(optional);

        for (var index = 0; index < optional.Length; index++)
        {
            var id = $"m{index}";
            database.ArchivedMatches.Add(Archived(id, Without(optional[index], id)));
        }

        await database.SaveChangesAsync();

        var page = await archive.GetFinishedAsync(MatchFilter.None, 0, 100, default);

        // A payload that fails to deserialise is dropped rather than thrown, so a row that went
        // missing here is the whole symptom: absence, not an error. Reporting the field name
        // rather than the row id means a failure says which field broke, not just that one did.
        string[] read = [.. page.Select(match => optional[int.Parse(match.Id[1..])]).Order()];

        Assert.Equal(optional, read);
    }

    [Fact]
    public void The_required_fields_of_an_archived_payload_are_pinned()
    {
        // Adding a name to this list is the change that orphans the archive, so it cannot be made
        // by accident. If a new field brought you here, give it a default instead of `required`.
        // If it genuinely must be required, every row already stored needs rewriting first.
        string[] pinned =
        [
            "Away",
            "Format",
            "HasBallByBall",
            "HasSquads",
            "Home",
            "Id",
            "MatchTitle",
            "SeriesName",
            "Slug",
            "StartTimeUtc",
            "Status",
            "StatusText",
            "Venue",
        ];

        Assert.Equal(pinned, Names(IsRequired));
    }

    [Fact]
    public async Task Series_are_tallied_from_the_rows_rather_than_listed()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("a", MatchStatus.Completed, Day(1), "Zimbabwe tri-series"),
                Match("b", MatchStatus.Completed, Day(2), "Ashes"),
                Match("c", MatchStatus.Completed, Day(3), "Ashes"),
            ],
            default);

        var tallies = await archive.GetSeriesTalliesAsync([], default);

        Assert.Equal(
            ["ashes", "zimbabwe-tri-series"],
            tallies.Select(tally => tally.SeriesId).OrderBy(id => id));

        var ashes = tallies.Single(tally => tally.SeriesId == "ashes");

        Assert.Equal(2, ashes.MatchCount);
        Assert.Equal(Day(2), ashes.FirstMatchUtc);
        Assert.Equal(Day(3), ashes.LastMatchUtc);

        // The archive holds finished matches and nothing else, so it can never report otherwise.
        Assert.False(ashes.HasUnfinished);
    }

    [Fact]
    public async Task A_series_reads_back_only_its_own_matches_in_playing_order()
    {
        var archive = Build();

        await archive.SaveFinishedAsync(
            [
                Match("b", MatchStatus.Completed, Day(3), "Ashes"),
                Match("a", MatchStatus.Completed, Day(1), "Ashes"),
                Match("other", MatchStatus.Completed, Day(2), "Zimbabwe tri-series"),
            ],
            default);

        var ashes = await archive.GetBySeriesAsync("ashes", default);

        Assert.Equal(["a", "b"], ashes.Select(match => match.Id));
    }

    [Fact]
    public async Task An_empty_series_id_collects_nothing_rather_than_everything()
    {
        var archive = Build();

        // Matches archived before the column existed have no series id. Treating that as a series
        // would gather unrelated matches under one page.
        await archive.SaveFinishedAsync([Match("a", MatchStatus.Completed, Day(1), string.Empty)], default);

        Assert.Empty(await archive.GetBySeriesAsync(string.Empty, default));
        Assert.Empty(await archive.GetBySeriesAsync("   ", default));
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

    /// <summary>A stored payload for <paramref name="id"/> with one property left out.</summary>
    private static string Without(string jsonName, string id)
    {
        using var document = JsonDocument.Parse(Json(Match(id, MatchStatus.Completed, Day(1))));

        var kept = new Dictionary<string, JsonElement>(
            document.RootElement
                .EnumerateObject()
                .Where(property => property.Name != jsonName)
                .Select(property => KeyValuePair.Create(property.Name, property.Value)));

        // Asserting the strip happened keeps a renamed field from turning this into a no-op test.
        Assert.Equal(document.RootElement.EnumerateObject().Count() - 1, kept.Count);

        return JsonSerializer.Serialize(kept, JsonSerializerOptions.Web);
    }

    /// <summary>A row written straight to the table, bypassing the writer as history has.</summary>
    private static ArchivedMatch Archived(string id, string payload) => new()
    {
        Id = id,
        Slug = id,
        StartTimeUtc = Day(1).UtcDateTime,
        SeriesId = string.Empty,
        SeriesName = "A series",
        HomeTeamId = "india",
        AwayTeamId = "australia",
        HomeTeamName = "India",
        AwayTeamName = "Australia",
        // As the writer would have set it for India and Australia: these rows bypass it, but they
        // still have to be readable for the assertions about their payloads to mean anything.
        InScope = true,
        Payload = payload,
        ArchivedAtUtc = Day(1).UtcDateTime,
    };

    /// <summary>The serialised names of every field the payload does not insist upon.</summary>
    private static string[] OptionalNames()
        => [.. Names(property => !IsRequired(property))
            .Select(JsonNamingPolicy.CamelCase.ConvertName)];

    private static string[] Names(Func<PropertyInfo, bool> keep)
        => [.. typeof(MatchDetailsDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(keep)
            .Select(property => property.Name)
            .Order()];

    private static bool IsRequired(PropertyInfo property)
        => property.GetCustomAttribute<RequiredMemberAttribute>() is not null;

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
            SeriesId = Slug.Kebab(series),
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
