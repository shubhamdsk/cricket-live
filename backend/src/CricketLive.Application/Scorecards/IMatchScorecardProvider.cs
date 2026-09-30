using CricketLive.Application.Enrichment;
using CricketLive.Application.Scorecards.Dtos;

namespace CricketLive.Application.Scorecards;

/// <summary>
/// Whatever can supply a scorecard, which by default is nothing.
/// </summary>
/// <remarks>
/// <para>
/// A second source, like <see cref="IMatchEnrichmentProvider"/>, and for the same reason: our
/// cricket provider does not serve scorecards at all. It is a separate interface rather than more
/// methods on <c>ICricketDataProvider</c> so that the match page keeps working unchanged when this
/// returns nothing, which is the normal case.
/// </para>
/// <para>
/// Takes a <see cref="MatchIdentity"/> rather than a bare id, because a second source has its own
/// identifiers and has to be told enough about a match to find it. The same reasoning as the
/// enrichment provider, written down in D-015.
/// </para>
/// </remarks>
public interface IMatchScorecardProvider
{
    /// <summary>
    /// The scorecard for a match, or <see langword="null"/> when there is none to be had.
    /// </summary>
    /// <remarks>
    /// Null covers every ordinary failure — the source is switched off, the match could not be
    /// paired with theirs, the request budget is spent, the page did not answer. None of those is
    /// exceptional and none should fail a match page, so this never throws for them. A caller
    /// renders no scorecard section rather than an error, which is D-013.
    /// </remarks>
    Task<ScorecardDto?> GetScorecardAsync(MatchIdentity match, CancellationToken cancellationToken);
}
