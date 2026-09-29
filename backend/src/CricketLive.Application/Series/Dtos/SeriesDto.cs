using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Series.Dtos;

/// <summary>
/// A series as the matches we hold describe it.
/// </summary>
/// <remarks>
/// <para>
/// Not as the provider describes it, which was a deliberate choice and is the thing to understand
/// about this type. CricketData's own <c>series</c> endpoint exists, and what it returns is an
/// index rather than data: of twenty-five series sampled, <c>endDate</c> was an ISO date in none
/// of them — always <c>"Apr 11"</c>, with the year left to be guessed from the series name — and
/// <c>squads</c> was zero in all twenty-five. Reading it would cost a call from a hundred-a-day
/// budget and buy a worse answer than counting the matches we already have in hand.
/// </para>
/// <para>
/// So every field here is derived, and <see cref="MatchCount"/> is the honest consequence: it is
/// how many matches of this series we can show, not how many it contains. A series we hold three
/// matches of reports three. The UI says so in those words rather than implying completeness.
/// </para>
/// </remarks>
public sealed record SeriesDto
{
    /// <summary>The provider's series id, shared by every match in it.</summary>
    public required string Id { get; init; }

    /// <summary>Readable identifier ending in <see cref="Id"/>, so a pretty URL stays resolvable.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Taken from the matches, which carry it as the tail of their own name.
    /// </summary>
    /// <remarks>
    /// Matches in one series occasionally disagree about it, the same way the provider writes an
    /// innings label two ways inside a single match. The longest spelling wins, on the grounds
    /// that the disagreements observed are truncations rather than different names.
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>When the earliest match we hold began.</summary>
    public required DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>When the latest match we hold began. Not when the series ends; we cannot know that.</summary>
    public required DateTimeOffset LastMatchUtc { get; init; }

    /// <summary>How many matches of this series we can show. See the remarks on this type.</summary>
    public required int MatchCount { get; init; }

    /// <summary>True when at least one match we hold has not finished.</summary>
    public required bool IsOngoing { get; init; }
}

/// <summary>A series together with its matches and, when one could be had, its standings.</summary>
public sealed record SeriesDetailsDto
{
    public required SeriesDto Series { get; init; }

    /// <summary>Every match of this series we hold, in playing order.</summary>
    public required IReadOnlyList<MatchDto> Matches { get; init; }

    /// <summary>
    /// The points table, when a source could supply one.
    /// </summary>
    /// <remarks>
    /// Empty is the normal case and means "we do not have one", never "the series has no table".
    /// Those are different claims and only the first is ours to make, so the UI renders absence as
    /// no section at all rather than as an empty table.
    /// </remarks>
    public IReadOnlyList<StandingDto> Standings { get; init; } = [];
}

/// <summary>One team's row in a points table, exactly as the source published it.</summary>
/// <remarks>
/// Every number here is read, never computed. Points rules differ by competition — the County
/// Championship alone awards bonus points for batting and bowling — so deriving a table from
/// results would mean inventing the rules and publishing the guess as fact.
/// </remarks>
public sealed record StandingDto
{
    /// <summary>
    /// The group this row belongs to, such as "Elite Group A", or empty when the competition has
    /// one table. A domestic league can publish four at once, and merging them would rank teams
    /// against opponents they never played.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// The team as the table names it, which is usually an abbreviation such as "MUM".
    /// </summary>
    /// <remarks>
    /// Not expanded to a full name. The expansion would be a lookup we do not have and would
    /// amount to guessing which "CDG" is meant, so the published string is shown as published.
    /// </remarks>
    public required string TeamName { get; init; }

    public required int Played { get; init; }

    public required int Won { get; init; }

    public required int Lost { get; init; }

    public required int Tied { get; init; }

    /// <summary>No result, which is not a tie and is counted separately in every format.</summary>
    public required int NoResult { get; init; }

    public required int Points { get; init; }

    /// <summary>Net run rate as published, as text: it is signed, and "-0.125" is not a number we round.</summary>
    public required string NetRunRate { get; init; }
}
