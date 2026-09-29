namespace CricketLive.Application.Matches.Dtos;

/// <summary>
/// A side and every innings it has batted. Tests hold two, limited-overs games one,
/// and a side that has not batted holds none.
/// </summary>
public sealed record TeamInningsDto(
    TeamDto Team,
    IReadOnlyList<InningsScoreDto> Innings);
