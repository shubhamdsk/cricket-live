using System.Text.RegularExpressions;
using CricketLive.Application.Enrichment;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Cricbuzz;

/// <summary>
/// Works out which Cricbuzz match is which of ours, from the site's own listing pages.
/// </summary>
/// <remarks>
/// One listing serves every match we might ask about, which is why this is worth doing at all: it
/// replaces a request per match with a request per cache period, and removes the need for somebody
/// to write pairs down by hand before a match can be enriched.
/// </remarks>
internal sealed partial class CricbuzzMatchDirectory(
    HttpClient client,
    IMemoryCache cache,
    IOptions<CricbuzzOptions> options,
    ILogger<CricbuzzMatchDirectory> logger)
{
    private const string CacheKey = "cricbuzz:directory";

    /// <summary>One caller refreshes the listing; the rest wait for it rather than each fetching.</summary>
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public async Task<string?> ResolveAsync(MatchIdentity match, CancellationToken cancellationToken)
    {
        var listings = await GetListingsAsync(cancellationToken);

        if (listings.Count == 0)
        {
            return null;
        }

        var resolved = CricbuzzSlug.Resolve(match, listings);

        if (resolved is null)
        {
            logger.LogDebug(
                "Cricbuzz listing does not identify exactly one match for {MatchTitle} / {SeriesName}",
                match.MatchTitle,
                match.SeriesName);
        }

        return resolved;
    }

    private async Task<IReadOnlyList<CricbuzzListing>> GetListingsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<CricbuzzListing>? cached) && cached is not null)
        {
            return cached;
        }

        await RefreshGate.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(CacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var listings = await FetchAsync(cancellationToken);

            // Cached even when empty, so a site that is refusing us is not asked again immediately.
            cache.Set(CacheKey, listings, TimeSpan.FromMinutes(options.Value.DirectoryCacheMinutes));
            return listings;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task<IReadOnlyList<CricbuzzListing>> FetchAsync(CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, CricbuzzListing>(StringComparer.Ordinal);

        foreach (var page in options.Value.ListingPaths)
        {
            string html;

            try
            {
                using var response = await client.GetAsync(page, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug(
                        "Cricbuzz listing {Page} returned {StatusCode}",
                        page,
                        (int)response.StatusCode);

                    continue;
                }

                html = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(exception, "Could not read the Cricbuzz listing at {Page}", page);
                continue;
            }

            foreach (var link in LinkPattern().Matches(html).Cast<Match>())
            {
                var id = link.Groups["id"].Value;

                // First page wins. The live listing is ordered first and is the more current of the
                // two, so a fixture appearing on both keeps its live entry.
                if (found.ContainsKey(id))
                {
                    continue;
                }

                var listing = CricbuzzSlug.Read(id, link.Groups["slug"].Value);

                if (listing is not null)
                {
                    found[id] = listing;
                }
            }
        }

        logger.LogInformation("Read {Count} match(es) from the Cricbuzz listing", found.Count);
        return [.. found.Values];
    }

    /// <summary>A scorecard link, as in <c>/live-cricket-scores/151543/ind-vs-wi-2nd-odi-…</c>.</summary>
    [GeneratedRegex(
        @"/live-cricket-scores/(?<id>\d{4,12})/(?<slug>[a-z0-9-]+)",
        RegexOptions.ExplicitCapture)]
    private static partial Regex LinkPattern();
}
