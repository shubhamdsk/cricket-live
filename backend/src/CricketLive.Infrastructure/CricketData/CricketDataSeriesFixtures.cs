using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Reads one series' full match list from <c>series_info</c>.
/// </summary>
/// <remarks>
/// <para>
/// One call per series, cached. The cost model is the point: the series list is sixty-odd entries
/// and this is only asked for the one somebody opened, so the page that benefits is the page that
/// pays. Reading every series up front would cost sixty calls out of a hundred a day to populate
/// pages nobody asked for.
/// </para>
/// <para>
/// <b>Failures are swallowed.</b> Same reasoning as <see cref="CricketDataSeriesIndex"/>: the
/// matches we hold already answer the question, and these are an addition. An addition that throws
/// would turn a slightly thin series page into no series page.
/// </para>
/// </remarks>
internal sealed class CricketDataSeriesFixtures(
    CricketDataClient client,
    CricketDataMatchMapper mapper,
    IMemoryCache cache,
    IOptions<CricketDataOptions> options,
    ILogger<CricketDataSeriesFixtures> logger) : ISeriesFixtures
{
    public async Task<IReadOnlyList<MatchDetailsDto>> GetAsync(
        string seriesId,
        CancellationToken cancellationToken)
    {
        // Refused here rather than sent, exactly as the match endpoint does: a malformed id cannot
        // name a series the provider has, and the allowance is only a hundred calls wide.
        if (!Slug.TryExtractId(seriesId, out var id))
        {
            return [];
        }

        var key = $"cricket-data:series-fixtures:{id}";

        if (cache.TryGetValue(key, out IReadOnlyList<MatchDetailsDto>? cached) && cached is not null)
        {
            return cached;
        }

        var matches = await ReadAsync(id, cancellationToken);

        cache.Set(key, matches, TimeSpan.FromHours(options.Value.SeriesFixturesCacheHours));

        return matches;
    }

    private async Task<IReadOnlyList<MatchDetailsDto>> ReadAsync(
        string id,
        CancellationToken cancellationToken)
    {
        CricketDataSeriesInfo? response;

        try
        {
            response = await client.GetAsync<CricketDataSeriesInfo>(
                "series_info",
                new Dictionary<string, string> { ["id"] = id },
                cancellationToken);
        }
        catch (CricketDataRejectedException rejection) when (!rejection.IsAccountProblem)
        {
            // A well-formed id the provider will not serve means there is no such series. Cached as
            // empty like any other answer, so a bad link does not cost a call every time it is hit.
            logger.LogInformation("Cricket data provider has no series {SeriesId}", id);
            return [];
        }
        catch (CricketDataUnavailableException exception)
        {
            logger.LogWarning(
                exception,
                "Could not read the fixture list for series {SeriesId}; serving none",
                id);

            return [];
        }
        catch (CricketDataRejectedException exception)
        {
            logger.LogWarning(
                exception,
                "The provider refused the fixture list for series {SeriesId}; serving none",
                id);

            return [];
        }

        var rows = response?.MatchList ?? [];

        foreach (var row in rows)
        {
            // The rows come back without a series_id — zero of eight, measured — and we already
            // know it, because it is what we asked with. Filling it in here rather than letting the
            // mapper write an empty one is what lets these matches be grouped like any others.
            row.SeriesId = id;
        }

        var matches = rows
            .Select(mapper.ToMatchDetails)
            .OfType<MatchDetailsDto>()
            .OrderBy(match => match.StartTimeUtc)
            .ToArray();

        logger.LogInformation(
            "Read {Count} match(es) for series {SeriesId} from the provider",
            matches.Length,
            id);

        return matches;
    }
}
