using System.Text.RegularExpressions;
using CricketLive.Application.Enrichment;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Cricbuzz;

/// <summary>
/// Fetches a Cricbuzz scorecard page and reads the batters at the crease out of its meta tags.
/// </summary>
/// <remarks>
/// <para>
/// This supplements the primary provider, it does not replace it. The source has no endpoint that
/// lists matches, so it cannot answer "what is on right now" and must be handed an id that came
/// from somewhere else. Score, teams, venue, format and status all stay with the primary provider,
/// which returns them reliably; only the batters come from here, because that is the single field
/// this source got right when tested against real pages.
/// </para>
/// <para>
/// It also cannot fail loudly. A second source is a second outage, and a match page missing two
/// player names is worth far more than a match page that will not load.
/// </para>
/// </remarks>
internal sealed partial class CricbuzzEnrichmentProvider(
    HttpClient client,
    IMemoryCache cache,
    IOptions<CricbuzzOptions> options,
    ILogger<CricbuzzEnrichmentProvider> logger) : IMatchEnrichmentProvider
{
    public async Task<IReadOnlyList<BatterDto>> GetCurrentBattersAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return [];
        }

        // An unlisted match is the normal case, not a failure. Most matches will never be mapped.
        if (!options.Value.MatchIds.TryGetValue(matchId, out var sourceMatchId))
        {
            return [];
        }

        if (!IsWellFormed(sourceMatchId))
        {
            // The id is interpolated into a URL, so this is the boundary that keeps a typo in
            // configuration from steering the request somewhere other than a scorecard.
            logger.LogWarning(
                "Cricbuzz match id configured for {MatchId} is not a plain number; ignoring it",
                matchId);

            return [];
        }

        var cacheKey = $"cricbuzz:batters:{sourceMatchId}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<BatterDto>? cached) && cached is not null)
        {
            return cached;
        }

        var batters = await ReadBattersAsync(sourceMatchId, cancellationToken);

        // Cached even when empty. A match with nobody batting, and a source that cannot be read,
        // both need to stop the next viewer causing another request.
        cache.Set(cacheKey, batters, TimeSpan.FromSeconds(options.Value.CacheSeconds));
        return batters;
    }

    private async Task<IReadOnlyList<BatterDto>> ReadBattersAsync(
        string sourceMatchId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(
                $"live-cricket-scores/{sourceMatchId}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug(
                    "Cricbuzz returned {StatusCode} for match {SourceMatchId}",
                    (int)response.StatusCode,
                    sourceMatchId);

                return [];
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var batters = CricbuzzOgTitle.ReadBatters(html);

            if (batters.Count == 0)
            {
                // Worth a line, because a page that loads but yields nobody is what a markup change
                // looks like from here, and it is indistinguishable from a break between innings.
                logger.LogDebug(
                    "Cricbuzz page for match {SourceMatchId} named no batters",
                    sourceMatchId);
            }

            return batters;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up; this is not a fault worth reporting.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(
                exception,
                "Could not reach Cricbuzz for match {SourceMatchId}; continuing without batters",
                sourceMatchId);

            return [];
        }
    }

    private static bool IsWellFormed(string? sourceMatchId)
        => sourceMatchId is not null && MatchIdPattern().IsMatch(sourceMatchId);

    /// <summary>A Cricbuzz match id, which is a short run of digits and nothing else.</summary>
    [GeneratedRegex(@"^\d{4,12}$")]
    private static partial Regex MatchIdPattern();
}
