using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Teams;
using CricketLive.Infrastructure.CricketData;
using CricketLive.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CricketLive.Infrastructure.Tests.Persistence;

public sealed class SqlWindowSnapshotStoreTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly CricketLiveDbContext database;
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly CricketDataOptions options = new()
    {
        WindowSnapshotMaxAgeHours = 12,
    };

    public SqlWindowSnapshotStoreTests()
    {
        connection.Open();

        database = new CricketLiveDbContext(
            new DbContextOptionsBuilder<CricketLiveDbContext>().UseSqlite(connection).Options);

        database.Database.EnsureCreated();
    }

    [Fact]
    public async Task A_saved_window_can_be_loaded_back()
    {
        var store = Build();
        var match = Match("m1", "https://g.cricapi.com/image/ind.png");

        await store.SaveAsync([match], clock.GetUtcNow(), default);

        var loaded = await store.LoadAsync(default);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Matches);
        Assert.Equal("m1", loaded.Matches[0].Id);
        Assert.Equal(clock.GetUtcNow(), loaded.CapturedAtUtc);
    }

    [Fact]
    public async Task Crest_URLs_are_rewritten_to_proxy_paths_on_load()
    {
        var store = Build();
        var match = Match("m1", "https://g.cricapi.com/image/ind.png");

        await store.SaveAsync([match], clock.GetUtcNow(), default);

        var loaded = await store.LoadAsync(default);

        Assert.NotNull(loaded);
        var homeLogo = loaded.Matches[0].Home.Team.LogoUrl;
        Assert.NotNull(homeLogo);
        Assert.StartsWith("/api/crests/", homeLogo);
    }

    [Fact]
    public async Task Saving_an_empty_window_does_not_overwrite_existing_snapshot()
    {
        var store = Build();
        var match = Match("m1", null);

        await store.SaveAsync([match], clock.GetUtcNow(), default);
        await store.SaveAsync([], clock.GetUtcNow().AddHours(1), default);

        var loaded = await store.LoadAsync(default);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Matches);
        Assert.Equal("m1", loaded.Matches[0].Id);
        Assert.Equal(clock.GetUtcNow(), loaded.CapturedAtUtc);
    }

    [Fact]
    public async Task A_second_save_updates_the_singleton_row()
    {
        var store = Build();
        var match1 = Match("m1", null);
        var match2 = Match("m2", null);

        await store.SaveAsync([match1], clock.GetUtcNow(), default);
        var newCapture = clock.GetUtcNow().AddHours(1);
        await store.SaveAsync([match2], newCapture, default);

        var rowCount = await database.WindowSnapshots.CountAsync();
        Assert.Equal(1, rowCount);

        var loaded = await store.LoadAsync(default);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Matches);
        Assert.Equal("m2", loaded.Matches[0].Id);
        Assert.Equal(newCapture, loaded.CapturedAtUtc);
    }

    [Fact]
    public async Task A_snapshot_older_than_max_age_is_refused()
    {
        var store = Build();
        var match = Match("m1", null);

        await store.SaveAsync([match], clock.GetUtcNow(), default);

        // Advance past the 12-hour limit
        clock.Advance(TimeSpan.FromHours(13));

        var loaded = await store.LoadAsync(default);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Loading_from_an_empty_table_returns_null()
    {
        var store = Build();

        var loaded = await store.LoadAsync(default);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Corrupted_payload_returns_null_without_throwing()
    {
        var store = Build();

        database.WindowSnapshots.Add(new WindowSnapshotRow
        {
            Id = WindowSnapshotRow.SingletonId,
            CapturedAtUtc = clock.GetUtcNow().UtcDateTime,
            Payload = "not valid json {[[",
        });
        await database.SaveChangesAsync();

        var loaded = await store.LoadAsync(default);

        Assert.Null(loaded);
    }

    public void Dispose()
    {
        database.Dispose();
        connection.Dispose();
    }

    private SqlWindowSnapshotStore Build()
        => new(
            database,
            Options.Create(options),
            clock,
            NullLogger<SqlWindowSnapshotStore>.Instance);

    private static MatchDetailsDto Match(string id, string? logoUrl)
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", logoUrl), []);

        return new MatchDetailsDto
        {
            Id = id,
            Slug = id,
            Status = MatchStatus.Completed,
            Format = MatchFormat.Odi,
            SeriesId = "series-a",
            SeriesName = "A series",
            MatchTitle = "1st ODI",
            Venue = "Somewhere",
            StartTimeUtc = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            Home = team,
            Away = team,
            StatusText = string.Empty,
            HasBallByBall = false,
            HasSquads = false,
        };
    }
}
