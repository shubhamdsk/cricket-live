namespace CricketLive.Application.Enrichment;

/// <summary>
/// Adds detail to a match that the primary data provider does not carry, for the one match a
/// client is actually watching.
/// </summary>
/// <remarks>
/// Everything here is optional by construction. Enrichment is a second source with a second set of
/// failure modes, and a match page that renders without it is worth far more than one that breaks
/// with it. Implementations return an empty result rather than throwing.
/// </remarks>
public interface IMatchEnrichmentProvider
{
    /// <summary>
    /// The batters at the crease, or an empty list when the source has none, cannot be reached, or
    /// returns something this cannot confidently read.
    /// </summary>
    /// <remarks>
    /// An empty list deliberately conflates "nobody is batting" with "the parse failed". Callers
    /// cannot act differently on the two, and inventing a distinction the source does not reliably
    /// signal would be inventing information.
    /// </remarks>
    /// <param name="match">
    /// Enough to recognise the fixture. Whether a second source knows this match, and by what
    /// identifier, is entirely the implementation's problem — nothing above Infrastructure should
    /// learn another provider's numbering scheme.
    /// </param>
    Task<IReadOnlyList<BatterDto>> GetCurrentBattersAsync(
        MatchIdentity match,
        CancellationToken cancellationToken);
}
