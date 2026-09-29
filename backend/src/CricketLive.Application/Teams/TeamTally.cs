namespace CricketLive.Application.Teams;

/// <summary>
/// What one source can say about a team from the matches it holds.
/// </summary>
/// <remarks>
/// <para>
/// The intermediate shape the window and the archive both report in, so <c>TeamService</c> merges
/// two comparable things rather than a list of matches against a set of SQL aggregates.
/// </para>
/// <para>
/// Note what is absent: won, lost, drawn. The provider states a result only as prose — "India won
/// by 8 wkts" — and turning that sentence into a record would mean parsing free text and then
/// publishing the parse as a team's history. A wrong record is worse than no record, so a team
/// reports what it played and nothing about how it fared.
/// </para>
/// </remarks>
public sealed record TeamTally
{
    /// <summary>The slug the team is addressed by, derived from its name.</summary>
    /// <remarks>
    /// Unlike a series, a team has no provider id — CricketData issues none — so identity comes
    /// from the name after all. The slug is at least stable under spacing and punctuation, and it
    /// is the same value <c>TeamDto.Id</c> already carried before any of this existed.
    /// </remarks>
    public required string TeamId { get; init; }

    public required string TeamName { get; init; }

    public required int MatchCount { get; init; }

    public required DateTimeOffset FirstMatchUtc { get; init; }

    public required DateTimeOffset LastMatchUtc { get; init; }

    /// <summary>Whether any of those matches has yet to finish.</summary>
    public required bool HasUnfinished { get; init; }

    /// <summary>
    /// The same team as seen by the other source, combined.
    /// </summary>
    /// <remarks>
    /// Counts add because the caller has already removed matches held in both places. The longer
    /// name wins for the same reason it does for a series: the shorter is usually the truncated one.
    /// </remarks>
    public TeamTally Merge(TeamTally other) => new()
    {
        TeamId = TeamId,
        TeamName = TeamName.Length >= other.TeamName.Length ? TeamName : other.TeamName,
        MatchCount = MatchCount + other.MatchCount,
        FirstMatchUtc = FirstMatchUtc <= other.FirstMatchUtc ? FirstMatchUtc : other.FirstMatchUtc,
        LastMatchUtc = LastMatchUtc >= other.LastMatchUtc ? LastMatchUtc : other.LastMatchUtc,
        HasUnfinished = HasUnfinished || other.HasUnfinished,
    };
}
