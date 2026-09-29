using CricketLive.Application.Common;
using CricketLive.Application.Teams;
using CricketLive.Application.Teams.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Teams)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
public sealed class TeamsController(ITeamService teams) : ControllerBase
{
    /// <summary>
    /// Every team we hold a match for.
    /// </summary>
    /// <remarks>
    /// Assembled from those matches, so this costs no provider call beyond the current-matches
    /// window every other list already shares. There is no team endpoint to call in any case.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TeamSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TeamSummaryDto>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await teams.GetAllAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<TeamSummaryDto>>.Ok(result));
    }

    /// <param name="teamId">
    /// The team's slug, such as <c>india</c>. There is no provider id to accept instead: the
    /// provider issues none for teams.
    /// </param>
    [HttpGet("{teamId}")]
    [ProducesResponseType<ApiResponse<TeamDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TeamDetailsDto>>> GetById(
        string teamId,
        CancellationToken cancellationToken)
    {
        var found = await teams.GetByIdAsync(teamId, cancellationToken);

        if (found is null)
        {
            return NotFound(ApiResponse<TeamDetailsDto>.Fail("Team not found."));
        }

        return Ok(ApiResponse<TeamDetailsDto>.Ok(found));
    }
}
