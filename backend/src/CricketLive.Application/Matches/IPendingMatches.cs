using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// Matches that are in progress or still to come, assembled from sources the provider's main
/// window does not cover.
/// </summary>
public interface IPendingMatches
{
    Task<IReadOnlyList<MatchDetailsDto>> GetAsync(CancellationToken cancellationToken);
}
