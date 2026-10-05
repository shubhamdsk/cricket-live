using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Teams;
using CricketLive.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CricketLive.Infrastructure.Tests.Persistence;

public sealed class SnapshottingCricketDataProviderTests
{
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly WindowFreshness freshness = new();

    [Fact]
    public async Task Fetching_the_window_saves_snapshot_and_leaves_freshness_unmarked()
    {
        var store = new MemoryWindowSnapshotStore();
        var match = Match("m1");
        var provider = Build(new StubProvider([match]), store);

        var result = await provider.GetCurrentMatchesAsync(default);

        Assert.Single(result);
        Assert.Equal("m1", result[0].Id);
        Assert.False(freshness.IsStale);
        Assert.Null(freshness.CapturedAtUtc);
        Assert.NotNull(store.SavedSnapshot);
        Assert.Equal(clock.GetUtcNow(), store.SavedSnapshot.CapturedAtUtc);
    }

    [Fact]
    public async Task When_provider_fails_and_snapshot_exists_it_serves_snapshot_and_marks_freshness()
    {
        var captureTime = clock.GetUtcNow().AddHours(-2);
        var storedMatch = Match("stored-1");
        var store = new MemoryWindowSnapshotStore
        {
            StoredSnapshot = new WindowSnapshot
            {
                Matches = [storedMatch],
                CapturedAtUtc = captureTime,
            },
        };
        var provider = Build(new ThrowingProvider(), store);

        var result = await provider.GetCurrentMatchesAsync(default);

        Assert.Single(result);
        Assert.Equal("stored-1", result[0].Id);
        Assert.True(freshness.IsStale);
        Assert.Equal(captureTime, freshness.CapturedAtUtc);
    }

    [Fact]
    public async Task When_provider_fails_and_no_snapshot_exists_it_rethrows()
    {
        var store = new MemoryWindowSnapshotStore { StoredSnapshot = null };
        var provider = Build(new ThrowingProvider(), store);

        await Assert.ThrowsAsync<CricketDataUnavailableException>(
            () => provider.GetCurrentMatchesAsync(default));

        Assert.False(freshness.IsStale);
    }

    [Fact]
    public async Task GetMatchAsync_delegates_to_inner_without_touching_snapshot_store()
    {
        var store = new MemoryWindowSnapshotStore();
        var match = Match("m1");
        var provider = Build(new StubProvider([match]), store);

        var result = await provider.GetMatchAsync("m1", default);

        Assert.NotNull(result);
        Assert.Equal("m1", result.Id);
        Assert.Null(store.SavedSnapshot);
    }

    private SnapshottingCricketDataProvider Build(ICricketDataProvider inner, IWindowSnapshotStore store)
        => new(
            inner,
            store,
            freshness,
            clock,
            NullLogger<SnapshottingCricketDataProvider>.Instance);

    private static MatchDetailsDto Match(string id)
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

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

    private sealed class StubProvider(IReadOnlyList<MatchDetailsDto> window) : ICricketDataProvider
    {
        public Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken)
            => Task.FromResult(window);

        public Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(window.FirstOrDefault(m => m.Id == matchId));
    }

    private sealed class ThrowingProvider : ICricketDataProvider
    {
        public Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken)
            => throw new CricketDataUnavailableException("Simulated provider outage");

        public Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
            => throw new CricketDataUnavailableException("Simulated provider outage");
    }

    private sealed class MemoryWindowSnapshotStore : IWindowSnapshotStore
    {
        public WindowSnapshot? StoredSnapshot { get; set; }
        public WindowSnapshot? SavedSnapshot { get; private set; }

        public Task SaveAsync(
            IReadOnlyList<MatchDetailsDto> matches,
            DateTimeOffset capturedAtUtc,
            CancellationToken cancellationToken)
        {
            SavedSnapshot = new WindowSnapshot
            {
                Matches = matches,
                CapturedAtUtc = capturedAtUtc,
            };
            return Task.CompletedTask;
        }

        public Task<WindowSnapshot?> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(StoredSnapshot);
    }
}
