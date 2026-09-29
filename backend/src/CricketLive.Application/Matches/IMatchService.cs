using CricketLive.Application.Common;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

public interface IMatchService
{
    Task<IReadOnlyList<MatchDto>> GetLiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A page of finished matches reaching back past the provider's window, and how many exist.
    /// </summary>
    /// <remarks>
    /// The count lets a client decide whether to offer another page. It grows over time rather than
    /// being a fixed archive depth, because history is kept as matches finish and not backfilled.
    /// </remarks>
    Task<(IReadOnlyList<MatchDto> Matches, int Total)> GetResultsAsync(
        PageRequest page,
        CancellationToken cancellationToken);

    Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken);
}
