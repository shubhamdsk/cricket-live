using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Matches)]
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

    /// <summary>
    /// Finished matches, reaching back past the provider's window into what we have kept.
    /// </summary>
    /// <param name="page">1-based. Values below 1 are treated as the first page.</param>
    /// <param name="pageSize">Clamped, because the caller does not get to decide how much we read.</param>
    [HttpGet("recent")]
    [ProducesResponseType<ApiResponse<PagedResult<MatchDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<MatchDto>>>> GetRecent(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultSize)
    {
        var requested = PageRequest.From(page, pageSize);

        var (result, total) = await matches.GetResultsAsync(requested, cancellationToken);

        return Ok(ApiResponse<PagedResult<MatchDto>>.Ok(
            PagedResult<MatchDto>.For(result, requested, total)));
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
