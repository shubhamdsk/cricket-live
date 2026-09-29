using CricketLive.Application.Common;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

public interface IMatchService
{
    Task<IReadOnlyList<MatchDto>> GetLiveAsync(MatchFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(MatchFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// A page of finished matches reaching back past the provider's window, and how many match.
    /// </summary>
    /// <remarks>
    /// The count lets a client decide whether to offer another page. Unfiltered it grows over time
    /// rather than being a fixed archive depth, because history is kept as matches finish and not
    /// backfilled.
    /// </remarks>
    Task<(IReadOnlyList<MatchDto> Matches, int Total)> GetResultsAsync(
        MatchFilter filter,
        PageRequest page,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every series that has a match behind it, so the filter cannot offer an empty selection.
    /// </summary>
    Task<IReadOnlyList<string>> GetSeriesNamesAsync(CancellationToken cancellationToken);

    Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken);
}
