using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Series.Dtos;

/// <summary>
/// A series described by the matches we hold, and listed by the provider's index.
/// </summary>
/// <remarks>
/// <para>
/// Two sources with different jobs, and the split is the thing to understand about this type. The
/// provider's <c>series</c> endpoint says which series <i>exist</i>; the matches we hold say what
/// can be <i>shown</i> about one. Originally only the second was read, on the grounds that the
/// index returns an index rather than data — true, and it remains true: <c>endDate</c> has never
/// once come back as an ISO date, always <c>"Apr 11"</c> with the year left to guess.
/// </para>
/// <para>
/// What that reasoning missed is that it made the list a function of the window. Outside a busy
/// fortnight the provider's window holds two or three matches, so the series page showed one
/// series and looked broken. The index costs a call and fixes exactly that, which is why it is now
/// read for existence and ignored for everything else.
/// </para>
/// <para>
/// Hence two counts, and they are not interchangeable. <see cref="MatchCount"/> is how many
/// matches we can put on the page; <see cref="TotalMatchCount"/> is how many the series has. The
/// UI prints both, because printing only the first invites it to be read as the second.
/// </para>
/// </remarks>
public sealed record SeriesDto
{
    /// <summary>The provider's series id, shared by every match in it.</summary>
    public required string Id { get; init; }

    /// <summary>Readable identifier ending in <see cref="Id"/>, so a pretty URL stays resolvable.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Taken from the matches, which carry it as the tail of their own name, or from the index.
    /// </summary>
    /// <remarks>
    /// Matches in one series occasionally disagree about it, the same way the provider writes an
    /// innings label two ways inside a single match. The longest spelling wins, on the grounds
    /// that the disagreements observed are truncations rather than different names. The index is
    /// treated as one more opinion under the same rule rather than as an authority.
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>
    /// When the earliest match we hold began, or when the series began for one we hold none of.
    /// </summary>
    /// <remarks>
    /// Those are different claims sharing a field, and the distinction is recoverable:
    /// <see cref="MatchCount"/> of zero means this is the index's date for the series itself. The
    /// two are not mixed for a single series — a series we hold matches of keeps its own dates, so
    /// the span this field starts never spans further than what we can actually show.
    /// </remarks>
    public required DateTimeOffset StartTimeUtc { get; init; }

    /// <summary>
    /// When the latest match we hold began, or <see langword="null"/> when we hold none.
    /// </summary>
    /// <remarks>
    /// Never when the series ends, which we cannot know: the index's <c>endDate</c> arrives without
    /// a year. Null is the honest value for a series that is listed but not held, and the UI shows
    /// a start date alone rather than a range it would have to invent the far end of.
    /// </remarks>
    public DateTimeOffset? LastMatchUtc { get; init; }

    /// <summary>How many matches of this series we can show. See the remarks on this type.</summary>
    public required int MatchCount { get; init; }

    /// <summary>
    /// How many matches the series has, as the provider's index counts them, or
    /// <see langword="null"/> when the index did not list it.
    /// </summary>
    /// <remarks>
    /// Null and zero mean different things and neither is "no matches". Null is "the index did not
    /// cover this series", which happens for anything outside the pages we read; zero never
    /// reaches here, because a row claiming no matches is dropped on the way in.
    /// </remarks>
    public int? TotalMatchCount { get; init; }

    /// <summary>
    /// True when at least one match we hold has not finished.
    /// </summary>
    /// <remarks>
    /// Only ever derived from held matches. A series known solely from the index reports false
    /// whatever its dates suggest, because deciding otherwise would mean comparing a start date
    /// against a clock and calling the result a fact.
    /// </remarks>
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
