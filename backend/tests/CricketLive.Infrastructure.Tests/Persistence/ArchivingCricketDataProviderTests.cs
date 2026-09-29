using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;
using CricketLive.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace CricketLive.Infrastructure.Tests.Persistence;

public sealed class ArchivingCricketDataProviderTests
{
    [Fact]
    public async Task Fetching_the_window_archives_it()
    {
        var archive = new RecordingArchive();
        var provider = Build(new StubProvider([Match("a")]), archive);

        await provider.GetCurrentMatchesAsync(default);

        Assert.Equal(["a"], archive.Saved.Select(match => match.Id));
    }

    [Fact]
    public async Task A_match_still_in_the_window_comes_from_the_provider()
    {
        var archive = new RecordingArchive { Held = Match("a") with { StatusText = "stale" } };
        var provider = Build(new StubProvider([Match("a")]), archive);

        var match = await provider.GetMatchAsync("a", default);

        Assert.Equal(string.Empty, match?.StatusText);
    }

    [Fact]
    public async Task A_match_the_window_has_dropped_comes_from_the_archive()
    {
        var archive = new RecordingArchive { Held = Match("gone") };
        var provider = Build(new StubProvider([]), archive);

        var match = await provider.GetMatchAsync("gone", default);

        Assert.Equal("gone", match?.Id);
    }

    [Fact]
    public async Task A_match_neither_source_has_is_still_absent()
    {
        var provider = Build(new StubProvider([]), new RecordingArchive());

        Assert.Null(await provider.GetMatchAsync("never-existed", default));
    }

    [Fact]
    public async Task A_failed_write_does_not_fail_the_read()
    {
        // The home page loading matters more than history being complete.
        var provider = Build(new StubProvider([Match("a")]), new ThrowingArchive());

        var window = await provider.GetCurrentMatchesAsync(default);

        Assert.Equal(["a"], window.Select(match => match.Id));
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed_as_a_failed_write()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var provider = Build(new StubProvider([Match("a")]), new ThrowingArchive());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetCurrentMatchesAsync(cancellation.Token));
    }

    private static ArchivingCricketDataProvider Build(ICricketDataProvider inner, IMatchArchive archive)
        => new(inner, archive, NullLogger<ArchivingCricketDataProvider>.Instance);

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
            StartTimeUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
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
            => Task.FromResult(window.FirstOrDefault(match => match.Id == matchId));
    }

    private sealed class RecordingArchive : IMatchArchive
    {
        public List<MatchDetailsDto> Saved { get; } = [];

        public MatchDetailsDto? Held { get; init; }

        public Task SaveFinishedAsync(IReadOnlyList<MatchDetailsDto> window, CancellationToken cancellationToken)
        {
            Saved.AddRange(window);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MatchDto>> GetFinishedAsync(
            MatchFilter filter,
            int skip,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(Held?.Id == matchId ? Held : null);

        public Task<int> CountFinishedAsync(MatchFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SeriesTally>>([]);

        public Task<IReadOnlyList<MatchDto>> GetBySeriesAsync(string seriesId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<IReadOnlyList<TeamTally>> GetTeamTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TeamTally>>([]);

        public Task<IReadOnlyList<MatchDto>> GetByTeamAsync(string teamId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);
    }

    private sealed class ThrowingArchive : IMatchArchive
    {
        public Task SaveFinishedAsync(IReadOnlyList<MatchDetailsDto> window, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The archive is unavailable");
        }

        public Task<IReadOnlyList<MatchDto>> GetFinishedAsync(
            MatchFilter filter,
            int skip,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult<MatchDetailsDto?>(null);

        public Task<int> CountFinishedAsync(MatchFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SeriesTally>>([]);

        public Task<IReadOnlyList<MatchDto>> GetBySeriesAsync(string seriesId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<IReadOnlyList<TeamTally>> GetTeamTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TeamTally>>([]);

        public Task<IReadOnlyList<MatchDto>> GetByTeamAsync(string teamId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);
    }
}
