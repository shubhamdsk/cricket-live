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
    public async Task<IReadOnlyList<MatchDto>> GetLiveAsync(CancellationToken cancellationToken)
    {
        var matches = await provider.GetCurrentMatchesAsync(cancellationToken);

        return [.. matches
            .Where(match => match.Status == MatchStatus.Live)
            .OrderBy(match => match.StartTimeUtc)];
    }

    public async Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(CancellationToken cancellationToken)
    {
        var matches = await provider.GetCurrentMatchesAsync(cancellationToken);

        return [.. matches
            .Where(match => match.Status == MatchStatus.Upcoming)
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
        PageRequest page,
        CancellationToken cancellationToken)
    {
        // Fetching the window is also what archives it, so by the time the archive is read below it
        // already holds everything the window could contribute. The archive is the source here.
        var window = await provider.GetCurrentMatchesAsync(cancellationToken);

        var stored = await archive.GetFinishedAsync(page.Skip, page.Take, cancellationToken);
        var total = await archive.CountFinishedAsync(cancellationToken);

        if (page.Skip > 0)
        {
            return (stored, total);
        }

        // Safety net, and only worth applying to the first page: if a write failed, a match that
        // finished minutes ago would disappear from results altogether rather than merely being
        // absent from history.
        var held = stored.Select(match => match.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rescued = window
            .Where(match => match.Status == MatchStatus.Completed && !held.Contains(match.Id))
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
