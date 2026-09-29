using CricketLive.Application.Enrichment;

namespace CricketLive.Application.Matches.Dtos;

/// <summary>
/// Everything a match card shows, plus what deeper data the provider claims to hold for this match.
/// Bowlers and commentary are absent because no source we have exposes them reliably — see docs/decisions.md.
/// </summary>
public sealed record MatchDetailsDto : MatchDto
{
    /// <summary>True when the provider flags ball-by-ball data as available for this match.</summary>
    public required bool HasBallByBall { get; init; }

    /// <summary>True when the provider flags squads as available for this match.</summary>
    public required bool HasSquads { get; init; }

    /// <summary>
    /// The batters at the crease, when a second source could supply them.
    /// </summary>
    /// <remarks>
    /// Empty far more often than not, and deliberately not <see langword="required"/>: this arrives
    /// from optional enrichment after the match is built, so every other construction site stays
    /// unaware of it. Empty means "we do not know", which callers must render as absence rather
    /// than as nobody batting.
    /// </remarks>
    public IReadOnlyList<BatterDto> CurrentBatters { get; init; } = [];
}
