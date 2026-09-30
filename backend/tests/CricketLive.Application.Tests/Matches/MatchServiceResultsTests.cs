using CricketLive.Application.Common;
using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Scorecards;
using CricketLive.Application.Scorecards.Dtos;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;

namespace CricketLive.Application.Tests.Matches;

/// <summary>
/// Covers the merge in <see cref="MatchService.GetResultsAsync"/>, which is the only place the
/// provider's short window and the archive's long memory have to agree with each other.
/// </summary>
public sealed class MatchServiceResultsTests
{
    [Fact]
    public async Task Results_come_from_the_archive_rather_than_the_window()
    {
        // Both hold the same match. If the window were also counted, it would appear twice.
        var match = Finished("a", Day(1));
        var service = Build(window: [match], archive: [match]);

        var (matches, total) = await service.GetResultsAsync(MatchFilter.None, PageRequest.From(1, 20), default);

        Assert.Equal(["a"], matches.Select(result => result.Id));
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task A_match_the_archive_missed_is_still_shown_on_the_first_page()
    {
        // The write failed, or the row is not there yet. Without the safety net the match would
        // vanish from results despite the provider having just told us it finished.
        var service = Build(window: [Finished("fresh", Day(2))], archive: [Finished("old", Day(1))]);

        var (matches, total) = await service.GetResultsAsync(MatchFilter.None, PageRequest.From(1, 20), default);

        Assert.Equal(["fresh", "old"], matches.Select(result => result.Id));
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task Only_finished_matches_are_rescued_from_the_window()
    {
        var service = Build(
            window: [Finished("done", Day(2)), Live("playing", Day(3)), Upcoming("later", Day(4))],
            archive: []);

        var (matches, _) = await service.GetResultsAsync(MatchFilter.None, PageRequest.From(1, 20), default);

        Assert.Equal(["done"], matches.Select(result => result.Id));
    }

    [Fact]
    public async Task Later_pages_read_the_archive_alone()
    {
        // The window only ever holds the last few days, so anything it could contribute belongs on
        // the first page. Adding it to page two would push an older match out for a newer one.
        var archived = Enumerable.Range(1, 3).Select(day => Finished($"a{day}", Day(day))).ToArray();
        var service = Build(window: [Finished("fresh", Day(9))], archive: archived);

        var (matches, total) = await service.GetResultsAsync(MatchFilter.None, PageRequest.From(2, 2), default);

        Assert.Equal(["a1"], matches.Select(result => result.Id));
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task A_rescued_match_does_not_push_the_page_over_its_size()
    {
        var archived = Enumerable.Range(1, 3).Select(day => Finished($"a{day}", Day(day))).ToArray();
        var service = Build(window: [Finished("fresh", Day(9))], archive: archived);

        var (matches, _) = await service.GetResultsAsync(MatchFilter.None, PageRequest.From(1, 3), default);

        Assert.Equal(3, matches.Count);
        Assert.Equal(["fresh", "a3", "a2"], matches.Select(result => result.Id));
    }

    [Fact]
    public async Task A_filter_narrows_the_window_and_the_archive_together()
    {
        var service = Build(
            window: [Finished("fresh", Day(9), "Kept")],
            archive: [Finished("old", Day(1), "Kept"), Finished("other", Day(2), "Discarded")]);

        var (matches, total) = await service.GetResultsAsync(
            new MatchFilter(SeriesName: "Kept"),
            PageRequest.From(1, 20),
            default);

        // "other" is excluded by the archive, and "fresh" survives the same filter on the window.
        Assert.Equal(["fresh", "old"], matches.Select(match => match.Id));
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task Asking_results_for_a_non_completed_status_answers_empty()
    {
        var service = Build(window: [Live("playing", Day(2))], archive: [Finished("old", Day(1))]);

        var (matches, total) = await service.GetResultsAsync(
            new MatchFilter(Status: MatchStatus.Live),
            PageRequest.From(1, 20),
            default);

        Assert.Empty(matches);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task A_status_filter_that_contradicts_the_list_answers_without_asking_the_provider()
    {
        // The saving here is a provider call, which is the scarce thing. A client sending one
        // filter to all three lists should not spend an API call on the two it excluded.
        var provider = new StubProvider([Live("playing", Day(2))]);
        var service = new MatchService(
            provider,
            new NoEnrichment(),
            new NoScorecards(),
            new StubArchive([]));

        var upcoming = await service.GetUpcomingAsync(new MatchFilter(Status: MatchStatus.Live), default);

        Assert.Empty(upcoming);
        Assert.Equal(0, provider.WindowCalls);
    }

    [Fact]
    public async Task Live_and_upcoming_honour_a_series_filter()
    {
        var service = Build(
            window: [Live("a", Day(1), "Kept"), Live("b", Day(2), "Discarded")],
            archive: []);

        var live = await service.GetLiveAsync(new MatchFilter(SeriesName: "Kept"), default);

        Assert.Equal(["a"], live.Select(match => match.Id));
    }

    [Fact]
    public async Task Series_names_come_from_both_the_window_and_the_archive()
    {
        // Neither source is a superset: the window has not heard of a tournament that ended last
        // week, and the archive has not heard of one that started this morning.
        var service = Build(
            window: [Live("a", Day(9), "Started this morning")],
            archive: [Finished("b", Day(1), "Ended last week")]);

        var series = await service.GetSeriesNamesAsync(default);

        Assert.Equal(["Ended last week", "Started this morning"], series);
    }

    private static MatchService Build(
        IReadOnlyList<MatchDetailsDto> window,
        IReadOnlyList<MatchDetailsDto> archive)
        => new(
            new StubProvider(window),
            new NoEnrichment(),
            new NoScorecards(),
            new StubArchive(archive));

    private static DateTimeOffset Day(int day) => new(2026, 1, day, 9, 0, 0, TimeSpan.Zero);

    private static MatchDetailsDto Finished(string id, DateTimeOffset start, string series = "A series")
        => Match(id, MatchStatus.Completed, start, series);

    private static MatchDetailsDto Live(string id, DateTimeOffset start, string series = "A series")
        => Match(id, MatchStatus.Live, start, series);

    private static MatchDetailsDto Upcoming(string id, DateTimeOffset start)
        => Match(id, MatchStatus.Upcoming, start);

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
            Slug = id,
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

    private sealed class StubProvider(IReadOnlyList<MatchDetailsDto> window) : ICricketDataProvider
    {
        /// <summary>Counted because a provider call is the scarce resource, not just a detail.</summary>
        public int WindowCalls { get; private set; }

        public Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken)
        {
            WindowCalls++;
            return Task.FromResult(window);
        }

        public Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(window.FirstOrDefault(match => match.Id == matchId));
    }

    /// <summary>
    /// An in-memory archive that applies the filter through <see cref="MatchFilter.Matches"/>.
    /// </summary>
    /// <remarks>
    /// Using the real predicate rather than a second copy of the rules is the point: a stub that
    /// filtered its own way would pass these tests while the SQL diverged.
    /// </remarks>
    private sealed class StubArchive(IReadOnlyList<MatchDetailsDto> held) : IMatchArchive
    {
        public Task SaveFinishedAsync(IReadOnlyList<MatchDetailsDto> window, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MatchDto>> GetFinishedAsync(
            MatchFilter filter,
            int skip,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>(
                [.. Filtered(filter).OrderByDescending(match => match.StartTimeUtc).Skip(skip).Take(take)]);

        public Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(held.FirstOrDefault(match => match.Id == matchId));

        public Task<int> CountFinishedAsync(MatchFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(Filtered(filter).Count());

        public Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SeriesTally>>(
            [
                .. held
                    .Where(match => !excluding.Contains(match.Id))
                    .GroupBy(match => new { match.SeriesId, match.SeriesName })
                    .Select(group => new SeriesTally
                    {
                        SeriesId = group.Key.SeriesId,
                        SeriesName = group.Key.SeriesName,
                        MatchCount = group.Count(),
                        FirstMatchUtc = group.Min(match => match.StartTimeUtc),
                        LastMatchUtc = group.Max(match => match.StartTimeUtc),
                        HasUnfinished = false,
                    })
            ]);

        public Task<IReadOnlyList<MatchDto>> GetBySeriesAsync(string seriesId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>(
                [.. held.Where(match => match.SeriesId == seriesId).OrderBy(match => match.StartTimeUtc)]);

        public Task<IReadOnlyList<TeamTally>> GetTeamTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TeamTally>>([]);

        public Task<IReadOnlyList<MatchDto>> GetByTeamAsync(string teamId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);

        private IEnumerable<MatchDetailsDto> Filtered(MatchFilter filter)
            => held.Where(filter.Matches);
    }

    private sealed class NoEnrichment : IMatchEnrichmentProvider
    {
        public Task<IReadOnlyList<BatterDto>> GetCurrentBattersAsync(
            MatchIdentity match,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BatterDto>>([]);
    }

    /// <summary>The disabled scorecard source, which is how this ships and what these tests want.</summary>
    private sealed class NoScorecards : IMatchScorecardProvider
    {
        public Task<ScorecardDto?> GetScorecardAsync(
            MatchIdentity match,
            CancellationToken cancellationToken)
            => Task.FromResult<ScorecardDto?>(null);
    }
}
