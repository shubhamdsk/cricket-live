using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Controllers;

[ApiController]
[Route("api/matches")]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
public sealed class MatchesController(IMatchService matches) : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MatchDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MatchDto>>>> GetLive(
        CancellationToken cancellationToken)
    {
        var result = await matches.GetLiveAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MatchDto>>.Ok(result));
    }

    [HttpGet("upcoming")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MatchDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MatchDto>>>> GetUpcoming(
        CancellationToken cancellationToken)
    {
        var result = await matches.GetUpcomingAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MatchDto>>.Ok(result));
    }

    [HttpGet("recent")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MatchDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MatchDto>>>> GetRecent(
        CancellationToken cancellationToken)
    {
        var result = await matches.GetRecentAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MatchDto>>.Ok(result));
    }

    /// <param name="matchId">Either the provider id or one of our slugs, which end in that id.</param>
    [HttpGet("{matchId}")]
    [ProducesResponseType<ApiResponse<MatchDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MatchDetailsDto>>> GetById(
        string matchId,
        CancellationToken cancellationToken)
    {
        var match = await matches.GetByIdAsync(matchId, cancellationToken);

        if (match is null)
        {
            return NotFound(ApiResponse<MatchDetailsDto>.Fail("Match not found."));
        }

        return Ok(ApiResponse<MatchDetailsDto>.Ok(match));
    }
}
