namespace CricketLive.Application.Scorecards.Dtos;

/// <summary>
/// A match's full scorecard: every innings, with both cards, the extras and how it fell apart.
/// </summary>
/// <remarks>
/// Separate from <see cref="Matches.Dtos.MatchDetailsDto"/> and served by its own endpoint, because
/// it comes from a different source with a different budget. Folding it into the match payload
/// would make every match page pay for a scorecard whether or not the reader scrolled to it.
/// </remarks>
public sealed class ScorecardDto
{
    /// <summary>Our match id, so a client can tell which match this belongs to.</summary>
    public required string MatchId { get; init; }

    /// <summary>Innings in the order they were played.</summary>
    public required IReadOnlyList<InningsCardDto> Innings { get; init; }

    /// <summary>
    /// The result or current state as the source words it.
    /// </summary>
    /// <remarks>
    /// Passed through rather than parsed. It is prose, and turning prose into a structured result
    /// is the guess this project declines to make everywhere else.
    /// </remarks>
    public required string Status { get; init; }

    /// <summary>Whether play has finished, which decides how long this can be cached.</summary>
    public required bool IsComplete { get; init; }
}

/// <summary>One innings, from both sides of it.</summary>
public sealed class InningsCardDto
{
    public required int InningsNumber { get; init; }

    /// <summary>The batting side's full name, and its short form for narrow screens.</summary>
    public required string BattingTeamName { get; init; }

    public required string BattingTeamShortName { get; init; }

    public required int Runs { get; init; }

    public required int Wickets { get; init; }

    /// <summary>Overs as a decimal, so <c>18.5</c> means eighteen overs and five balls.</summary>
    public required double Overs { get; init; }

    public required double RunRate { get; init; }

    public required bool IsDeclared { get; init; }

    /// <summary>Everyone who batted, in the order they came in.</summary>
    public required IReadOnlyList<BattingLineDto> Batting { get; init; }

    /// <summary>Everyone who bowled, in the order they were used.</summary>
    public required IReadOnlyList<BowlingLineDto> Bowling { get; init; }

    public required ExtrasDto Extras { get; init; }

    /// <summary>Wickets in the order they fell.</summary>
    public required IReadOnlyList<WicketDto> FallOfWickets { get; init; }

    /// <summary>Stands in the order they happened, including the one still going.</summary>
    public required IReadOnlyList<PartnershipDto> Partnerships { get; init; }
}

public sealed class BattingLineDto
{
    public required string Name { get; init; }

    public required int Runs { get; init; }

    public required int Balls { get; init; }

    public required int Fours { get; init; }

    public required int Sixes { get; init; }

    /// <summary>
    /// Runs per hundred balls, as the source reports it.
    /// </summary>
    /// <remarks>
    /// A string, because that is what arrives and because recomputing it from runs and balls would
    /// produce a different number for a batter who has faced none — a divide by zero we would then
    /// have to invent an answer for.
    /// </remarks>
    public required string StrikeRate { get; init; }

    /// <summary>
    /// How they got out, or "not out", or empty for someone yet to bat.
    /// </summary>
    /// <remarks>
    /// Kept as the source's own wording — <c>"c Tanush Kotian b Shams Mulani"</c>. It is the one
    /// field here that is genuinely prose, and every attempt to structure it loses information.
    /// </remarks>
    public required string Dismissal { get; init; }

    public required bool IsCaptain { get; init; }

    public required bool IsKeeper { get; init; }
}

public sealed class BowlingLineDto
{
    public required string Name { get; init; }

    /// <summary>Overs bowled, as a string because <c>3.5</c> means three overs and five balls.</summary>
    public required string Overs { get; init; }

    public required int Maidens { get; init; }

    public required int Runs { get; init; }

    public required int Wickets { get; init; }

    public required string Economy { get; init; }
}

public sealed class ExtrasDto
{
    public required int Byes { get; init; }

    public required int LegByes { get; init; }

    public required int Wides { get; init; }

    public required int NoBalls { get; init; }

    public required int Penalty { get; init; }

    public required int Total { get; init; }
}

public sealed class WicketDto
{
    public required string BatterName { get; init; }

    /// <summary>The team's score when this wicket fell.</summary>
    public required int Runs { get; init; }

    /// <summary>Which wicket this was, counting from one.</summary>
    public required int WicketNumber { get; init; }

    /// <summary>The over it fell in, as a decimal.</summary>
    public required double Over { get; init; }
}

public sealed class PartnershipDto
{
    public required string FirstBatterName { get; init; }

    public required int FirstBatterRuns { get; init; }

    public required string SecondBatterName { get; init; }

    public required int SecondBatterRuns { get; init; }

    public required int Runs { get; init; }

    public required int Balls { get; init; }
}
