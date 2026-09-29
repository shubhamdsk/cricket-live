using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Enrichment;

/// <summary>
/// Attaches optional detail to a match.
/// </summary>
/// <remarks>
/// Shared by the request path and the background poller so a match cannot arrive enriched over one
/// and bare over the other, which would show a client its batters vanishing on every push.
/// </remarks>
public static class Enrich
{
    public static async Task<MatchDetailsDto> WithBattersAsync(
        MatchDetailsDto match,
        IMatchEnrichmentProvider enrichment,
        CancellationToken cancellationToken)
    {
        var batters = await enrichment.GetCurrentBattersAsync(match.Id, cancellationToken);

        // Returning the original instance when there is nothing to add keeps the common case free
        // of an allocation, and keeps reference equality meaningful for callers that compare.
        return batters.Count == 0 ? match : match with { CurrentBatters = batters };
    }
}
