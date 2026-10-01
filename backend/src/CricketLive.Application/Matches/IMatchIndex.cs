namespace CricketLive.Application.Matches;

/// <summary>
/// A wider, thinner view of which matches are around right now than
/// <see cref="ICricketDataProvider.GetCurrentMatchesAsync"/> gives.
/// </summary>
/// <remarks>
/// <para>
/// Measured on the same minute, the provider's main window held 2 matches and both were finished,
/// while this one held 6 and 4 of them were still to be played. That difference is the entire
/// reason this exists: the upcoming list was empty not because there is no cricket coming but
/// because the endpoint we asked does not mention it.
/// </para>
/// <para>
/// Thinner in the way that matters: an entry names its series but carries no series id, no venue
/// and no match title, and writes its teams as <c>"India [IND]"</c>. So this is an index in the
/// same sense as <see cref="Series.ISeriesIndex"/> — it answers <i>which</i>, and something else
/// has to answer <i>what</i>.
/// </para>
/// </remarks>
public interface IMatchIndex
{
    Task<IReadOnlyList<MatchIndexEntry>> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One match the index knows of, reduced to the three things it can be trusted for.
/// </summary>
public sealed record MatchIndexEntry
{
    /// <summary>The provider's match id, which is the same id its other endpoints use.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// The series name as the index writes it. Not an id — the index does not carry one, which is
    /// why resolving it means looking the name up somewhere that does.
    /// </summary>
    public required string SeriesName { get; init; }

    /// <summary>
    /// <see langword="true"/> once the match is over.
    /// </summary>
    /// <remarks>
    /// Derived by asking whether the provider's state field says the match is finished, rather than
    /// by listing the states that mean it is not. An unrecognised state therefore reads as pending,
    /// which is the safe direction: a finished match shown as upcoming would be corrected by the
    /// detail the fixture source fills in, whereas an upcoming match read as finished is dropped
    /// and never looked at again.
    /// </remarks>
    public required bool IsFinished { get; init; }
}

/// <summary>
/// No index. The upcoming list falls back to whatever the main window happens to contain.
/// </summary>
public sealed class NoMatchIndex : IMatchIndex
{
    public Task<IReadOnlyList<MatchIndexEntry>> GetAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<MatchIndexEntry>>([]);
}
