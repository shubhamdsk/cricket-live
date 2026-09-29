namespace CricketLive.Application.Matches.Dtos;

/// <summary>
/// Everything a match card shows, plus what deeper data the provider claims to hold for this match.
/// Batters, bowlers and commentary are absent because the provider does not expose them — see docs/decisions.md.
/// </summary>
public sealed record MatchDetailsDto : MatchDto
{
    /// <summary>True when the provider flags ball-by-ball data as available for this match.</summary>
    public required bool HasBallByBall { get; init; }

    /// <summary>True when the provider flags squads as available for this match.</summary>
    public required bool HasSquads { get; init; }
}
