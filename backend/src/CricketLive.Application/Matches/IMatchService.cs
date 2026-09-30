using CricketLive.Application.Common;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Scorecards.Dtos;

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

    /// <summary>
    /// The scorecard for a match, or <see langword="null"/> when there is none to be had.
    /// </summary>
    /// <remarks>
    /// Its own call rather than a field on <see cref="MatchDetailsDto"/>, so a reader who never
    /// scrolls to the scorecard never spends a call fetching one. Null covers both "no such match"
    /// and "no scorecard for this match", which a client renders the same way: no section.
    /// </remarks>
    Task<ScorecardDto?> GetScorecardAsync(string matchId, CancellationToken cancellationToken);
}
