using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;
using CricketLive.Application.Series.Dtos;

namespace CricketLive.Application.Tests.Series;

/// <summary>
/// Covers the join that makes a series: matches of one live in two places, and neither place has
/// all of them.
/// </summary>
public sealed class SeriesServiceTests
{
    /// <summary>
    /// Provider series ids are GUIDs, and the tests use real ones because the service refuses
    /// anything else before it looks anywhere. A friendlier fake id would pass through code the
    /// production path never reaches.
    /// </summary>
    private const string Ashes = "66b6a70a-a9a5-4c79-b98f-ffe3a2006613";

    private const string Other = "dfe730c5-873d-44ce-955c-00d82716749b";

    [Fact]
    public async Task A_series_is_identified_by_the_provider_id_rather_than_the_name()
    {
        // The name reaches us as the tail of a free-text field, and the provider does not always
        // spell it the same way twice. Grouping by name would split this series in two.
        var service = Build(
            window: [Match("a", Ashes, "The Ashes 2026-27", Day(2))],
            archive: [Match("b", Ashes, "The Ashes", Day(1))]);

        var all = await service.GetAllAsync(default);

        var series = Assert.Single(all);
        Assert.Equal(Ashes, series.Id);
        Assert.Equal(2, series.MatchCount);
    }

    [Fact]
    public async Task The_fuller_spelling_of_a_name_wins()
    {
        var service = Build(
            window: [Match("a", Ashes, "The Ashes", Day(2))],
            archive: [Match("b", Ashes, "The Ashes 2026-27", Day(1))]);

        var series = Assert.Single(await service.GetAllAsync(default));

        Assert.Equal("The Ashes 2026-27", series.Name);
    }

    [Fact]
    public async Task A_series_spans_both_sources_without_counting_a_match_twice()
    {
        // A match that finished minutes ago is in the window and in the archive at the same time.
        var both = Match("a", Ashes, "The Ashes", Day(1));

        var service = Build(window: [both], archive: [both]);

        var details = await service.GetByIdAsync(Ashes, default);

        Assert.NotNull(details);
        Assert.Equal(["a"], details.Matches.Select(match => match.Id));
    }

    [Fact]
    public async Task Matches_of_a_series_are_returned_in_playing_order()
    {
        var service = Build(
            window: [Match("late", Ashes, "The Ashes", Day(5))],
            archive: [Match("early", Ashes, "The Ashes", Day(1))]);

        var details = await service.GetByIdAsync(Ashes, default);

        Assert.NotNull(details);
        Assert.Equal(["early", "late"], details.Matches.Select(match => match.Id));
    }

    [Fact]
    public async Task A_match_with_no_series_id_belongs_to_no_series()
    {
        // Grouping these together would invent a series out of unrelated matches.
        var service = Build(
            window: [Match("a", string.Empty, "Unknown", Day(1))],
            archive: []);

        Assert.Empty(await service.GetAllAsync(default));
    }

    [Fact]
    public async Task A_series_we_hold_nothing_of_is_not_found()
    {
        var service = Build(window: [], archive: []);

        Assert.Null(await service.GetByIdAsync(Ashes, default));
    }

    [Fact]
    public async Task Something_that_is_not_an_identifier_is_refused_without_a_lookup()
    {
        var provider = new StubProvider([]);
        var service = new SeriesService(provider, new StubArchive([]), new NoSeriesStandingsProvider());

        Assert.Null(await service.GetByIdAsync("../etc/passwd", default));

        // The id is malformed, so there was nothing worth asking either source about.
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task A_slug_resolves_to_the_series_it_ends_with()
    {
        var id = Guid.NewGuid().ToString();
        var service = Build(window: [Match("a", id, "The Ashes", Day(1))], archive: []);

        var details = await service.GetByIdAsync($"the-ashes-{id}", default);

        Assert.NotNull(details);
        Assert.Equal(id, details.Series.Id);
    }

    [Fact]
    public async Task An_unfinished_match_makes_the_series_ongoing_and_sorts_it_first()
    {
        var service = Build(
            window: [Match("a", Ashes, "Happening now", Day(9), MatchStatus.Live)],
            archive: [Match("b", Other, "Finished last week", Day(1))]);

        var all = await service.GetAllAsync(default);

        Assert.Equal([Ashes, Other], all.Select(series => series.Id));
        Assert.True(all[0].IsOngoing);
        Assert.False(all[1].IsOngoing);
    }

    [Fact]
    public async Task The_dates_span_every_match_we_hold_of_the_series()
    {
        var service = Build(
            window: [Match("b", Ashes, "The Ashes", Day(5))],
            archive: [Match("a", Ashes, "The Ashes", Day(1))]);

        var series = Assert.Single(await service.GetAllAsync(default));

        Assert.Equal(Day(1), series.StartTimeUtc);
        Assert.Equal(Day(5), series.LastMatchUtc);
    }

    [Fact]
    public async Task Standings_are_absent_rather_than_empty_when_nothing_supplies_them()
    {
        var service = Build(window: [Match("a", Ashes, "The Ashes", Day(1))], archive: []);

        var details = await service.GetByIdAsync(Ashes, default);

        Assert.NotNull(details);
        Assert.Empty(details.Standings);
    }

    private static SeriesService Build(
        IReadOnlyList<MatchDetailsDto> window,
        IReadOnlyList<MatchDetailsDto> archive)
        => new(new StubProvider(window), new StubArchive(archive), new NoSeriesStandingsProvider());

    private static DateTimeOffset Day(int day) => new(2026, 1, day, 9, 0, 0, TimeSpan.Zero);

    private static MatchDetailsDto Match(
        string id,
        string seriesId,
        string seriesName,
        DateTimeOffset start,
        MatchStatus status = MatchStatus.Completed)
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

        return new MatchDetailsDto
        {
            Id = id,
            Slug = id,
            Status = status,
            Format = MatchFormat.Odi,
            SeriesId = seriesId,
            SeriesName = seriesName,
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
        public int Calls { get; private set; }

        public Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(window);
        }

        public Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(window.FirstOrDefault(match => match.Id == matchId));
    }

    private sealed class StubArchive(IReadOnlyList<MatchDetailsDto> held) : IMatchArchive
    {
        public Task SaveFinishedAsync(IReadOnlyList<MatchDetailsDto> window, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MatchDto>> GetFinishedAsync(
            MatchFilter filter,
            int skip,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(held.FirstOrDefault(match => match.Id == matchId));

        public Task<int> CountFinishedAsync(MatchFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(
            IReadOnlyCollection<string> excluding,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SeriesTally>>(
            [
                .. held
                    .Where(match => !excluding.Contains(match.Id))
                    .Where(match => !string.IsNullOrWhiteSpace(match.SeriesId))
                    .GroupBy(match => match.SeriesId)
                    .Select(group => new SeriesTally
                    {
                        SeriesId = group.Key,
                        SeriesName = group.Max(match => match.SeriesName) ?? string.Empty,
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
    }
}
