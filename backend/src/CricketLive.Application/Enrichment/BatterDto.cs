namespace CricketLive.Application.Enrichment;

/// <summary>A batter currently at the crease.</summary>
/// <remarks>
/// Runs and balls are integers rather than the source's <c>46(74)</c> text because they are
/// genuine counts. Overs stay a string elsewhere in the codebase for the opposite reason: <c>12.3</c>
/// is twelve overs and three balls, and is not a number you may do arithmetic on.
/// </remarks>
public sealed record BatterDto(string Name, int Runs, int Balls);
