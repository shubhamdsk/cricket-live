using CricketLive.Application.Teams.Dtos;

namespace CricketLive.Application.Teams;

/// <summary>Teams, as the matches we hold describe them.</summary>
public interface ITeamService
{
    /// <summary>Every team we hold a match for, most recently seen first.</summary>
    Task<IReadOnlyList<TeamSummaryDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One team and its matches, or <see langword="null"/> when we hold none.
    /// </summary>
    /// <param name="idOrSlug">
    /// The team's slug. Unlike a match or a series there is no provider id behind it, so the slug
    /// is the identifier rather than a readable wrapper around one.
    /// </param>
    Task<TeamDetailsDto?> GetByIdAsync(string idOrSlug, CancellationToken cancellationToken);
}
