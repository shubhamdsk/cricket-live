using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// Turns the provider's single current-matches window into the three lists the UI asks for.
/// Splitting here rather than upstream is what keeps our request cost at one call per refresh.
/// </summary>
public sealed class MatchService(
    ICricketDataProvider provider,
    IMatchEnrichmentProvider enrichment) : IMatchService
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

    public async Task<IReadOnlyList<MatchDto>> GetRecentAsync(CancellationToken cancellationToken)
    {
        var matches = await provider.GetCurrentMatchesAsync(cancellationToken);

        // Most recently finished first, which is the order a results list is read in.
        return [.. matches
            .Where(match => match.Status == MatchStatus.Completed)
            .OrderByDescending(match => match.StartTimeUtc)];
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
