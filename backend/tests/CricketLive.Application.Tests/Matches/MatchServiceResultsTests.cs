using CricketLive.Application.Common;
using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;

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

        var (matches, total) = await service.GetResultsAsync(PageRequest.From(1, 20), default);

        Assert.Equal(["a"], matches.Select(result => result.Id));
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task A_match_the_archive_missed_is_still_shown_on_the_first_page()
    {
        // The write failed, or the row is not there yet. Without the safety net the match would
        // vanish from results despite the provider having just told us it finished.
        var service = Build(window: [Finished("fresh", Day(2))], archive: [Finished("old", Day(1))]);

        var (matches, total) = await service.GetResultsAsync(PageRequest.From(1, 20), default);

        Assert.Equal(["fresh", "old"], matches.Select(result => result.Id));
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task Only_finished_matches_are_rescued_from_the_window()
    {
        var service = Build(
            window: [Finished("done", Day(2)), Live("playing", Day(3)), Upcoming("later", Day(4))],
            archive: []);

        var (matches, _) = await service.GetResultsAsync(PageRequest.From(1, 20), default);

        Assert.Equal(["done"], matches.Select(result => result.Id));
    }

    [Fact]
    public async Task Later_pages_read_the_archive_alone()
    {
        // The window only ever holds the last few days, so anything it could contribute belongs on
        // the first page. Adding it to page two would push an older match out for a newer one.
        var archived = Enumerable.Range(1, 3).Select(day => Finished($"a{day}", Day(day))).ToArray();
        var service = Build(window: [Finished("fresh", Day(9))], archive: archived);

        var (matches, total) = await service.GetResultsAsync(PageRequest.From(2, 2), default);

        Assert.Equal(["a1"], matches.Select(result => result.Id));
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task A_rescued_match_does_not_push_the_page_over_its_size()
    {
        var archived = Enumerable.Range(1, 3).Select(day => Finished($"a{day}", Day(day))).ToArray();
        var service = Build(window: [Finished("fresh", Day(9))], archive: archived);

        var (matches, _) = await service.GetResultsAsync(PageRequest.From(1, 3), default);

        Assert.Equal(3, matches.Count);
        Assert.Equal(["fresh", "a3", "a2"], matches.Select(result => result.Id));
    }

    private static MatchService Build(
        IReadOnlyList<MatchDetailsDto> window,
        IReadOnlyList<MatchDetailsDto> archive)
        => new(new StubProvider(window), new NoEnrichment(), new StubArchive(archive));

    private static DateTimeOffset Day(int day) => new(2026, 1, day, 9, 0, 0, TimeSpan.Zero);

    private static MatchDetailsDto Finished(string id, DateTimeOffset start)
        => Match(id, MatchStatus.Completed, start);

    private static MatchDetailsDto Live(string id, DateTimeOffset start)
        => Match(id, MatchStatus.Live, start);

    private static MatchDetailsDto Upcoming(string id, DateTimeOffset start)
        => Match(id, MatchStatus.Upcoming, start);

    private static MatchDetailsDto Match(string id, MatchStatus status, DateTimeOffset start)
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

        return new MatchDetailsDto
        {
            Id = id,
            Slug = id,
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

    private sealed class StubProvider(IReadOnlyList<MatchDetailsDto> window) : ICricketDataProvider
    {
        public Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken)
            => Task.FromResult(window);

        public Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(window.FirstOrDefault(match => match.Id == matchId));
    }

    private sealed class StubArchive(IReadOnlyList<MatchDetailsDto> held) : IMatchArchive
    {
        public Task SaveFinishedAsync(IReadOnlyList<MatchDetailsDto> window, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<MatchDto>> GetFinishedAsync(int skip, int take, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MatchDto>>(
                [.. held.OrderByDescending(match => match.StartTimeUtc).Skip(skip).Take(take)]);

        public Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult(held.FirstOrDefault(match => match.Id == matchId));

        public Task<int> CountFinishedAsync(CancellationToken cancellationToken)
            => Task.FromResult(held.Count);
    }

    private sealed class NoEnrichment : IMatchEnrichmentProvider
    {
        public Task<IReadOnlyList<BatterDto>> GetCurrentBattersAsync(
            MatchIdentity match,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BatterDto>>([]);
    }
}
