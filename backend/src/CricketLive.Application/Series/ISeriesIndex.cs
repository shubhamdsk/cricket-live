namespace CricketLive.Application.Series;

/// <summary>
/// The provider's own list of series, read for existence and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="CricketLive.Application.Matches.ICricketDataProvider"/> on purpose.
/// That interface is the route match data takes, and losing it means the site has nothing to show;
/// this one is additive, and losing it means a shorter list. Keeping them apart is what lets the
/// implementation here swallow a provider outage instead of propagating it.
/// </para>
/// <para>
/// This exists because a series assembled only from matches we hold can only ever be a series we
/// hold a match of. For most of a year that is one or two, which reads as a broken page rather
/// than as the honest consequence of a narrow window that it is.
/// </para>
/// </remarks>
public interface ISeriesIndex
{
    /// <summary>
    /// Every series the provider lists within the pages we are willing to pay for, or an empty
    /// list when it could not be read.
    /// </summary>
    /// <remarks>
    /// Empty never means "there are no series". It means we have nothing to add, and callers must
    /// treat it as silence rather than as an answer — the series derived from held matches stand
    /// on their own and are not filtered against this.
    /// </remarks>
    Task<IReadOnlyList<SeriesIndexEntry>> GetAsync(CancellationToken cancellationToken);
}

/// <summary>What the index knows about one series, which is little but is more than nothing.</summary>
public sealed record SeriesIndexEntry
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>When the series began, as the provider dates it. Not when a match we hold began.</summary>
    public required DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>
    /// How many matches the provider holds for this series.
    /// </summary>
    /// <remarks>
    /// The whole series, not our slice of it, and the difference is the point: it is what lets a
    /// page say "2 of 8 held" rather than "2 matches" and leave the reader to assume that is all
    /// there were.
    /// </remarks>
    public required int MatchCount { get; init; }
}

/// <summary>Used when the index is switched off, so the series list falls back to held matches.</summary>
/// <remarks>
/// A real implementation returning nothing rather than a null check at the call site, matching
/// <c>NoSeriesStandingsProvider</c>. The absence of a source is a configuration, not a special case.
/// </remarks>
public sealed class NoSeriesIndex : ISeriesIndex
{
    public Task<IReadOnlyList<SeriesIndexEntry>> GetAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<SeriesIndexEntry>>([]);
}
