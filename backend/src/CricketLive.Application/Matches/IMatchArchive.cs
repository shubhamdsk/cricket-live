using CricketLive.Application.Matches.Dtos;

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
    /// Every distinct series the archive holds a match for.
    /// </summary>
    /// <remarks>
    /// Read from the rows rather than kept as a list, so the filter can only ever offer series
    /// that really have matches behind them. A fixed list would go stale the first time a
    /// tournament ended, and would offer selections that return nothing.
    /// </remarks>
    Task<IReadOnlyList<string>> GetSeriesNamesAsync(CancellationToken cancellationToken);
}
