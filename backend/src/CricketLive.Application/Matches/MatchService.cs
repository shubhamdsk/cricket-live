using CricketLive.Application.Common;
using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// Turns the provider's single current-matches window into the three lists the UI asks for.
/// Splitting here rather than upstream is what keeps our request cost at one call per refresh.
/// </summary>
public sealed class MatchService(
    ICricketDataProvider provider,
    IMatchEnrichmentProvider enrichment,
    IMatchArchive archive) : IMatchService
{
    public Task<IReadOnlyList<MatchDto>> GetLiveAsync(
        MatchFilter filter,
        CancellationToken cancellationToken)
        => FromWindowAsync(MatchStatus.Live, filter, cancellationToken);

    public Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(
        MatchFilter filter,
        CancellationToken cancellationToken)
        => FromWindowAsync(MatchStatus.Upcoming, filter, cancellationToken);

    /// <summary>
    /// One status out of the provider's window, narrowed by whatever else the caller asked for.
    /// </summary>
    /// <remarks>
    /// Filtered in memory rather than upstream because the window is a single response holding a
    /// handful of matches and the provider offers no query parameters worth the call. The archive
    /// filters in SQL for the opposite reason — it is the list that grows.
    /// </remarks>
    private async Task<IReadOnlyList<MatchDto>> FromWindowAsync(
        MatchStatus status,
        MatchFilter filter,
        CancellationToken cancellationToken)
    {
        // A caller asking for live matches while filtering to completed ones is contradicting
        // itself. Empty is the honest answer, and it costs no provider call to say so.
        if (filter.Status is { } wanted && wanted != status)
        {
            return [];
        }

        var matches = await provider.GetCurrentMatchesAsync(cancellationToken);

        return [.. matches
            .Where(match => match.Status == status && filter.Matches(match))
            .OrderBy(match => match.StartTimeUtc)];
    }

    /// <summary>
    /// Finished matches from the provider's window and from everything we kept before it moved on.
    /// </summary>
    /// <remarks>
    /// The window is only a few days wide, so on its own it answers "what happened today" and
    /// nothing more. Merging is needed rather than reading the archive alone because a match that
    /// finished minutes ago is in the window before it is anywhere else.
    /// </remarks>
    public async Task<(IReadOnlyList<MatchDto> Matches, int Total)> GetResultsAsync(
        MatchFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (filter.Status is { } wanted && wanted != MatchStatus.Completed)
        {
            return ([], 0);
        }

        // Fetching the window is also what archives it, so by the time the archive is read below it
        // already holds everything the window could contribute. The archive is the source here.
        var window = await provider.GetCurrentMatchesAsync(cancellationToken);

        var stored = await archive.GetFinishedAsync(filter, page.Skip, page.Take, cancellationToken);
        var total = await archive.CountFinishedAsync(filter, cancellationToken);

        if (page.Skip > 0)
        {
            return (stored, total);
        }

        // Safety net, and only worth applying to the first page: if a write failed, a match that
        // finished minutes ago would disappear from results altogether rather than merely being
        // absent from history.
        var held = stored.Select(match => match.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rescued = window
            .Where(match => match.Status == MatchStatus.Completed
                && !held.Contains(match.Id)
                && filter.Matches(match))
            .ToArray();

        if (rescued.Length == 0)
        {
            return (stored, total);
        }

        var merged = rescued
            .Concat<MatchDto>(stored)
            .OrderByDescending(match => match.StartTimeUtc)
            .Take(page.Take)
            .ToArray();

        return (merged, total + rescued.Length);
    }

    /// <summary>
    /// Every series we can actually show a match for, from both places matches live.
    /// </summary>
    /// <remarks>
    /// Both sources, because either alone would offer a filter that finds nothing. The archive
    /// knows nothing about a tournament that started this morning, and the window has forgotten
    /// one that finished last week. Neither is a superset of the other.
    /// </remarks>
    public async Task<IReadOnlyList<string>> GetSeriesNamesAsync(CancellationToken cancellationToken)
    {
        var window = await provider.GetCurrentMatchesAsync(cancellationToken);
        var archived = await archive.GetSeriesTalliesAsync(cancellationToken);

        return
        [
            .. window
                .Select(match => match.SeriesName)
                .Concat(archived.Select(tally => tally.SeriesName))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken)
    {
        var match = await provider.GetMatchAsync(matchId, cancellationToken);

        // Only a match in progress has anyone at the crease, and only the detail view shows them,
        // so this is the one place and the one moment enrichment is worth a second request.
        if (match is null || match.Status != MatchStatus.Live)
        {
            return match;
        }

        return await Enrich.WithBattersAsync(match, enrichment, cancellationToken);
    }
}
