using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;

namespace CricketLive.Application.Matches;

/// <summary>
/// Keeps finished matches after the provider stops returning them.
/// </summary>
/// <remarks>
/// <para>
/// The provider serves a narrow window — live, imminent and recently finished — and a match that
/// ages out of it is simply gone. Nothing here fetches anything; it keeps what we were already
/// given, so history costs no extra provider calls and is the provider's own data unaltered.
/// </para>
/// <para>
/// It therefore accumulates forward. Matches played before this existed cannot appear, and
/// pretending otherwise would mean inventing results.
/// </para>
/// </remarks>
public interface IMatchArchive
{
    /// <summary>
    /// Records every finished match in a provider window, leaving already-recorded ones untouched.
    /// </summary>
    /// <remarks>
    /// Only finished matches, because they cannot change again. Archiving one still in play would
    /// freeze a score mid-innings and then have to reconcile it.
    /// </remarks>
    Task SaveFinishedAsync(IReadOnlyList<MatchDetailsDto> window, CancellationToken cancellationToken);

    /// <summary>Finished matches, most recently started first.</summary>
    /// <remarks>
    /// Filtering happens in the store rather than over the returned page, because a page filtered
    /// after the fact is a page with holes in it: asking for twenty and discarding nine leaves
    /// eleven, and the count no longer agrees with what came back.
    /// </remarks>
    Task<IReadOnlyList<MatchDto>> GetFinishedAsync(
        MatchFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    /// <summary>A single archived match, or <see langword="null"/> when it was never recorded.</summary>
    Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken);

    /// <summary>How many finished matches match, so a client knows whether more exist.</summary>
    Task<int> CountFinishedAsync(MatchFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Every series the archive holds a match for, with what the rows say about each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the rows rather than kept as a list, so a series can only be offered when it
    /// really has matches behind it. A fixed list would go stale the first time a tournament
    /// ended, and would offer selections that return nothing.
    /// </para>
    /// <para>
    /// Aggregated in SQL over the indexed columns, so this reads no payloads. It answers both the
    /// filter's question — which series exist — and the series list's, which is what each one
    /// contains, and answering them separately would mean two definitions of "a series we have".
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(CancellationToken cancellationToken);

    /// <summary>Archived matches belonging to one series, earliest first.</summary>
    Task<IReadOnlyList<MatchDto>> GetBySeriesAsync(string seriesId, CancellationToken cancellationToken);
}
