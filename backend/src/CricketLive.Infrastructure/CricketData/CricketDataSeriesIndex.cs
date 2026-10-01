using System.Globalization;
using CricketLive.Application.Matches;
using CricketLive.Application.Series;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Reads the provider's <c>series</c> index, a bounded number of pages at a time.
/// </summary>
/// <remarks>
/// <para>
/// The index is 1190 series deep and twenty-five to a page, so reading all of it costs 48 of a
/// hundred daily calls for one list. It is read newest-first — the provider orders it by start
/// date descending — which is what makes a bounded read sensible rather than arbitrary: the first
/// few pages are the current season, and the pages we skip are history nobody opened this page
/// looking for.
/// </para>
/// <para>
/// <b>Failure here is swallowed, unlike every other provider call in this project.</b> A series
/// list built from held matches is correct without the index; it is merely short. Propagating an
/// outage would turn a shorter list into no page at all, which is strictly worse. The exception is
/// logged and the last good answer is served when there is one.
/// </para>
/// </remarks>
internal sealed class CricketDataSeriesIndex(
    CricketDataClient client,
    IMemoryCache cache,
    IOptions<CricketDataOptions> options,
    ILogger<CricketDataSeriesIndex> logger) : ISeriesIndex
{
    private const string Key = "cricket-data:series-index";
    private const string LastKnownGoodKey = "cricket-data:series-index:last-known-good";
    private const int PageSize = 25;

    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public async Task<IReadOnlyList<SeriesIndexEntry>> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key, out IReadOnlyList<SeriesIndexEntry>? cached) && cached is not null)
        {
            return cached;
        }

        // One caller pays for the pages; the rest wait and share them. Without this, a cold cache
        // and three simultaneous readers would spend the page budget three times over.
        await RefreshGate.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(Key, out cached) && cached is not null)
            {
                return cached;
            }

            var entries = await ReadPagesAsync(cancellationToken);

            cache.Set(Key, entries, TimeSpan.FromHours(options.Value.SeriesIndexCacheHours));
            cache.Set(LastKnownGoodKey, entries, TimeSpan.FromHours(options.Value.SeriesIndexCacheHours * 4));

            return entries;
        }
        catch (CricketDataUnavailableException exception)
        {
            var stale = cache.TryGetValue(LastKnownGoodKey, out IReadOnlyList<SeriesIndexEntry>? held)
                ? held
                : null;

            logger.LogWarning(
                exception,
                "The series index could not be read; serving {Count} series from the last good response",
                stale?.Count ?? 0);

            return stale ?? [];
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    /// <summary>
    /// Pages sequentially, stopping as soon as the provider runs out or the budget we allowed does.
    /// </summary>
    /// <remarks>
    /// Sequential rather than concurrent, deliberately. The calls are cheap individually and the
    /// budget is claimed per call, so firing them together would only make the moment we exceed
    /// the allowance harder to see in a log.
    /// </remarks>
    private async Task<IReadOnlyList<SeriesIndexEntry>> ReadPagesAsync(CancellationToken cancellationToken)
    {
        var entries = new List<SeriesIndexEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var page = 0; page < options.Value.SeriesIndexPages; page++)
        {
            var rows = await client.GetAsync<List<CricketDataSeries>>(
                "series",
                new Dictionary<string, string>
                {
                    ["offset"] = (page * PageSize).ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken);

            if (rows is null || rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                // seen guards against the provider listing one series twice, which it does: the
                // same tour appears as a stub with no matches and again with its fixtures. The
                // filter below drops the stub, but the id is the thing that must stay unique.
                if (ToEntry(row) is { } entry && seen.Add(entry.Id))
                {
                    entries.Add(entry);
                }
            }

            if (rows.Count < PageSize)
            {
                break;
            }
        }

        logger.LogInformation(
            "Read {Count} series from the provider index across up to {Pages} page(s)",
            entries.Count,
            options.Value.SeriesIndexPages);

        return entries;
    }

    /// <summary>
    /// Returns <see langword="null"/> for a row we cannot stand behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two conditions, and they turn out to be the same condition. A row needs a start date we can
    /// place on a calendar, and it needs at least one match for a page about it to have anything on
    /// it. Across fifty rows sampled, every row with matches had a full ISO date and every row
    /// without had the year-less <c>"Oct 18"</c> form, so requiring both costs nothing that
    /// requiring either would have kept.
    /// </para>
    /// <para>
    /// What this does drop is a genuinely upcoming series whose fixtures the provider has not
    /// loaded yet — "Sri Lanka tour of India, 2026" was listed with zero matches and a start of
    /// <c>"Dec 13"</c>. Listing it would mean inventing a year for that date and then opening a
    /// page with nothing on it. It reappears by itself once the provider attaches fixtures.
    /// </para>
    /// </remarks>
    private static SeriesIndexEntry? ToEntry(CricketDataSeries row)
    {
        if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Name) || row.Matches <= 0)
        {
            return null;
        }

        if (!DateOnly.TryParseExact(
                row.StartDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var start))
        {
            return null;
        }

        return new SeriesIndexEntry
        {
            Id = row.Id.Trim(),
            Name = row.Name.Trim(),
            // A date with no time of day. Midnight UTC is the only reading that does not invent
            // one, and it is only ever used to order and to print a day.
            StartTimeUtc = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            MatchCount = row.Matches,
        };
    }
}
