using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

public interface IMatchService
{
    Task<IReadOnlyList<MatchDto>> GetLiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MatchDto>> GetRecentAsync(CancellationToken cancellationToken);

    Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken);
}
