using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Reads cricket from api.cricapi.com. Every design choice here exists to spend as few calls as
/// possible: one upstream request serves all three lists, the detail endpoint is only used for
/// matches outside that window, and concurrent callers share a single in-flight request.
/// </summary>
internal sealed class CricketDataProvider(
    CricketDataClient client,
    CricketDataMatchMapper mapper,
    IMemoryCache cache,
    IOptions<CricketDataOptions> options,
    ILogger<CricketDataProvider> logger) : ICricketDataProvider
{
    private const string CurrentMatchesKey = "cricket-data:current-matches";
    private const string LastKnownGoodKey = "cricket-data:current-matches:last-known-good";
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public async Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken)
        => await GetCurrentDetailsAsync(cancellationToken);

    public async Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
    {
        if (!TryExtractId(matchId, out var id))
        {
            // Refusing a malformed id here is not only correct, it stops a stream of junk requests
            // from spending a daily allowance that is only a hundred calls wide.
            return null;
        }

        // The current-matches response already carries every field the detail endpoint does, so a
        // match that is in the window costs nothing extra to serve.
        var current = await GetCurrentDetailsAsync(cancellationToken);
        var known = current.FirstOrDefault(match =>
            string.Equals(match.Id, id, StringComparison.OrdinalIgnoreCase));

        if (known is not null)
        {
            return known;
        }

        var cacheKey = $"cricket-data:match:{id}";
        if (cache.TryGetValue(cacheKey, out MatchDetailsDto? cached))
        {
            return cached;
        }

        CricketDataMatch? response;

        try
        {
            response = await client.GetAsync<CricketDataMatch>(
                "match_info",
                new Dictionary<string, string> { ["id"] = id },
                cancellationToken);
        }
        catch (CricketDataRejectedException rejection) when (!rejection.IsAccountProblem)
        {
            // A well-formed id the provider will not serve means there is no such match.
            logger.LogInformation("Cricket data provider has no match {MatchId}", id);
            return null;
        }
        catch (CricketDataRejectedException rejection)
        {
            throw new CricketDataUnavailableException(
                "The cricket data provider refused the request.",
                rejection);
        }

        if (response is null)
        {
            return null;
        }

        var match = mapper.ToMatchDetails(response);
        if (match is null)
        {
            return null;
        }

        cache.Set(cacheKey, match, CacheLifetimeFor(match.Status));
        return match;
    }

    private async Task<IReadOnlyList<MatchDetailsDto>> GetCurrentDetailsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CurrentMatchesKey, out IReadOnlyList<MatchDetailsDto>? cached) && cached is not null)
        {
            return cached;
        }

        // One caller refreshes; the rest wait and take the result rather than each spending a call.
        await RefreshGate.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(CurrentMatchesKey, out cached) && cached is not null)
            {
                return cached;
            }

            List<CricketDataMatch>? response;

            try
            {
                response = await client.GetAsync<List<CricketDataMatch>>(
                    "currentMatches",
                    new Dictionary<string, string> { ["offset"] = "0" },
                    cancellationToken);
            }
            catch (CricketDataRejectedException rejection)
            {
                // Nothing about this list can be "not found", so any refusal is an outage to us.
                throw new CricketDataUnavailableException(
                    "The cricket data provider refused the request.",
                    rejection);
            }

            var matches = (response ?? [])
                .Select(mapper.ToMatchDetails)
                .OfType<MatchDetailsDto>()
                .ToArray();

            cache.Set(
                CurrentMatchesKey,
                (IReadOnlyList<MatchDetailsDto>)matches,
                TimeSpan.FromSeconds(options.Value.CurrentMatchesCacheSeconds));

            // Kept far longer than the live entry, purely so an outage has something to fall back on.
            cache.Set(
                LastKnownGoodKey,
                (IReadOnlyList<MatchDetailsDto>)matches,
                TimeSpan.FromHours(options.Value.FinishedMatchCacheHours));

            return matches;
        }
        catch (CricketDataUnavailableException) when (
            cache.TryGetValue(LastKnownGoodKey, out IReadOnlyList<MatchDetailsDto>? stale) && stale is not null)
        {
            logger.LogWarning(
                "Serving {Count} cricket matches from the last known good response because the provider is unavailable",
                stale.Count);

            return stale;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private TimeSpan CacheLifetimeFor(MatchStatus status) => status == MatchStatus.Completed
        ? TimeSpan.FromHours(options.Value.FinishedMatchCacheHours)
        : TimeSpan.FromSeconds(options.Value.CurrentMatchesCacheSeconds);

    private static bool TryExtractId(string? idOrSlug, out string id)
        => Slug.TryExtractId(idOrSlug, out id);
}
