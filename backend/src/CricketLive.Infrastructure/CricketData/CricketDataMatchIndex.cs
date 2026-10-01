using CricketLive.Application.Matches;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Reads the provider's <c>cricScore</c> endpoint for the ids of matches around now.
/// </summary>
/// <remarks>
/// <para>
/// One call, no paging, and a wider answer than the main window gives. Measured within the same
/// minute: <c>currentMatches</c> returned 2 rows and both were finished, <c>cricScore</c> returned
/// 6 of which 4 were fixtures yet to be played. The provider documents no relationship between the
/// two and we found none — neither is a subset of the other, so both are read.
/// </para>
/// <para>
/// <b>Failure is swallowed</b>, as with the two series sources: this is an addition to the main
/// window and losing it costs a shorter upcoming list, not a working site.
/// </para>
/// </remarks>
internal sealed class CricketDataMatchIndex(
    CricketDataClient client,
    IMemoryCache cache,
    IOptions<CricketDataOptions> options,
    ILogger<CricketDataMatchIndex> logger) : IMatchIndex
{
    private const string Key = "cricket-data:match-index";

    public async Task<IReadOnlyList<MatchIndexEntry>> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key, out IReadOnlyList<MatchIndexEntry>? cached) && cached is not null)
        {
            return cached;
        }

        var entries = await ReadAsync(cancellationToken);

        cache.Set(Key, entries, TimeSpan.FromMinutes(options.Value.MatchIndexCacheMinutes));

        return entries;
    }

    private async Task<IReadOnlyList<MatchIndexEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        List<CricketDataScoreboardRow>? rows;

        try
        {
            rows = await client.GetAsync<List<CricketDataScoreboardRow>>("cricScore", null, cancellationToken);
        }
        catch (CricketDataUnavailableException exception)
        {
            logger.LogWarning(exception, "Could not read the match index; serving none");
            return [];
        }
        catch (CricketDataRejectedException exception)
        {
            logger.LogWarning(exception, "The provider refused the match index; serving none");
            return [];
        }

        var entries = (rows ?? [])
            .Select(ToEntry)
            .OfType<MatchIndexEntry>()
            .ToArray();

        logger.LogInformation(
            "Match index holds {Count} match(es), {Pending} of them unfinished",
            entries.Length,
            entries.Count(entry => !entry.IsFinished));

        return entries;
    }

    /// <summary>
    /// Returns <see langword="null"/> for a row that cannot be joined to anything.
    /// </summary>
    /// <remarks>
    /// Both fields are required because both are load-bearing: without the id the row cannot be
    /// matched to a real match, and without the series name there is nothing to look up to find
    /// one. A row missing either is not a thin match, it is unusable.
    /// </remarks>
    private static MatchIndexEntry? ToEntry(CricketDataScoreboardRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Series))
        {
            return null;
        }

        return new MatchIndexEntry
        {
            Id = row.Id.Trim(),
            SeriesName = row.Series.Trim(),
            // Asking whether it says "result" rather than whether it says "fixture", so a state we
            // have not seen — live, abandoned, anything the provider adds later — reads as pending
            // and gets looked at, instead of being quietly dropped off the upcoming list.
            IsFinished = string.Equals(row.MatchState, "result", StringComparison.OrdinalIgnoreCase),
        };
    }
}
