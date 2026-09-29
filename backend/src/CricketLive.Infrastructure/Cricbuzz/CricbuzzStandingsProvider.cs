using System.Text.RegularExpressions;
using CricketLive.Application.Series;
using CricketLive.Application.Series.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Cricbuzz;

/// <summary>
/// Reads a series points table from Cricbuzz, when switched on.
/// </summary>
/// <remarks>
/// <para>
/// Standings are the one part of a series we can neither derive nor buy: CricketData publishes no
/// table at all, and computing one from results would mean encoding each competition's points
/// rules — the County Championship alone awards bonus points for batting and bowling — and then
/// presenting the guess as fact. So it is read from a site that publishes one, or it is absent.
/// </para>
/// <para>
/// <b>This is taken against Cricbuzz's stated preference,</b> which disallows every agent its
/// <c>robots.txt</c> has not named, and ours is not named. That is a deliberate decision recorded
/// in docs/decisions.md as D-020, and it is why this is off by default, cached for hours rather
/// than seconds, and never retried. It is also why the user agent stays honest: declining a
/// preference and impersonating a permitted crawler are different acts.
/// </para>
/// <para>
/// Every failure returns an empty list. A missing table costs the reader one section of a page;
/// a wrong one tells them a team is top when it is not.
/// </para>
/// </remarks>
internal sealed partial class CricbuzzStandingsProvider(
    HttpClient client,
    IMemoryCache cache,
    IOptions<CricbuzzOptions> options,
    ILogger<CricbuzzStandingsProvider> logger) : ISeriesStandingsProvider
{
    private const string DirectoryKey = "cricbuzz:series-directory";

    /// <summary>One caller refreshes the listing; the rest wait rather than each fetching it.</summary>
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public async Task<IReadOnlyList<StandingDto>> GetAsync(
        SeriesDto series,
        CancellationToken cancellationToken)
    {
        if (!options.Value.StandingsEnabled || string.IsNullOrWhiteSpace(series.Name))
        {
            return [];
        }

        var cacheKey = $"cricbuzz:standings:{series.Id}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<StandingDto>? cached) && cached is not null)
        {
            return cached;
        }

        var standings = await ReadAsync(series, cancellationToken);

        // Cached even when empty, so a series that has no table — most of them, since a two-team
        // tour never does — is not looked up again on every view of its page.
        cache.Set(cacheKey, standings, TimeSpan.FromMinutes(options.Value.StandingsCacheMinutes));

        return standings;
    }

    private async Task<IReadOnlyList<StandingDto>> ReadAsync(
        SeriesDto series,
        CancellationToken cancellationToken)
    {
        var path = await ResolveAsync(series, cancellationToken);

        if (path is null)
        {
            return [];
        }

        var html = await GetStringAsync($"{path}/points-table", cancellationToken);
        var standings = CricbuzzPointsTable.Parse(html);

        logger.LogInformation(
            "Read {Count} standings row(s) for {SeriesName}",
            standings.Count,
            series.Name);

        return standings;
    }

    /// <summary>
    /// The Cricbuzz path for this series, or <see langword="null"/> when the listing does not
    /// identify exactly one.
    /// </summary>
    /// <remarks>
    /// The two sites happen to slugify a series name the same way, which is what makes this work
    /// without anything written down by hand. It is still a join between two providers, so it
    /// follows the rule D-016 established for matches: a unique answer or no answer. Loading the
    /// wrong tournament's table is worse than loading none.
    /// </remarks>
    private async Task<string?> ResolveAsync(SeriesDto series, CancellationToken cancellationToken)
    {
        var wanted = CricbuzzSlug.Slugify(series.Name);

        if (wanted.Length == 0)
        {
            return null;
        }

        var listing = await GetDirectoryAsync(cancellationToken);

        var matches = listing
            .Where(entry => string.Equals(entry.Slug, wanted, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
        {
            logger.LogDebug(
                "Cricbuzz listing identifies {Count} series for {Slug}; declining",
                matches.Length,
                wanted);

            return null;
        }

        return $"cricket-series/{matches[0].Id}/{matches[0].Slug}";
    }

    private async Task<IReadOnlyList<CricbuzzSeriesLink>> GetDirectoryAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(DirectoryKey, out IReadOnlyList<CricbuzzSeriesLink>? cached) && cached is not null)
        {
            return cached;
        }

        await RefreshGate.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(DirectoryKey, out cached) && cached is not null)
            {
                return cached;
            }

            var links = await FetchDirectoryAsync(cancellationToken);

            // Cached even when empty, so a site that is refusing us is not asked again at once.
            cache.Set(DirectoryKey, links, TimeSpan.FromMinutes(options.Value.DirectoryCacheMinutes));

            return links;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private async Task<IReadOnlyList<CricbuzzSeriesLink>> FetchDirectoryAsync(CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, CricbuzzSeriesLink>(StringComparer.Ordinal);

        foreach (var page in options.Value.SeriesListingPaths)
        {
            var html = await GetStringAsync(page, cancellationToken);

            if (html is null)
            {
                continue;
            }

            foreach (var link in SeriesLink().Matches(html).Cast<Match>())
            {
                var id = link.Groups["id"].Value;
                found.TryAdd(id, new CricbuzzSeriesLink(id, link.Groups["slug"].Value));
            }
        }

        logger.LogInformation("Read {Count} series link(s) from the Cricbuzz listing", found.Count);

        return [.. found.Values];
    }

    /// <summary>
    /// One page, or <see langword="null"/> when it could not be read.
    /// </summary>
    /// <remarks>
    /// Deliberately no retry. This reads someone else's website against their stated preference,
    /// and a page that did not answer is not an invitation to ask again.
    /// </remarks>
    private async Task<string?> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Cricbuzz {Path} returned {StatusCode}", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Could not read Cricbuzz {Path}", path);
            return null;
        }
    }

    /// <summary>A series link, as in <c>/cricket-series/12398/ranji-trophy-elite-2026-27</c>.</summary>
    [GeneratedRegex(
        @"/cricket-series/(?<id>\d{2,12})/(?<slug>[a-z0-9-]+)",
        RegexOptions.ExplicitCapture)]
    private static partial Regex SeriesLink();
}

/// <summary>One series as Cricbuzz's own listing links to it.</summary>
internal sealed record CricbuzzSeriesLink(string Id, string Slug);
