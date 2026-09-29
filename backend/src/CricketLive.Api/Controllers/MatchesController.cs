using CricketLive.Api.Contracts;
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
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MatchDto>>>> GetLive(
        [FromQuery] MatchFilterQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryToFilter(out var filter, out var error))
        {
            return BadRequest(ApiResponse<IReadOnlyList<MatchDto>>.Fail(error!));
        }

        var result = await matches.GetLiveAsync(filter, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MatchDto>>.Ok(result));
    }

    [HttpGet("upcoming")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MatchDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MatchDto>>>> GetUpcoming(
        [FromQuery] MatchFilterQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.TryToFilter(out var filter, out var error))
        {
            return BadRequest(ApiResponse<IReadOnlyList<MatchDto>>.Fail(error!));
        }

        var result = await matches.GetUpcomingAsync(filter, cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<MatchDto>>.Ok(result));
    }

    /// <summary>
    /// Every series a match can currently be filtered to.
    /// </summary>
    /// <remarks>
    /// Read from the matches themselves rather than kept as a list, so the filter can only offer
    /// selections that have something behind them.
    /// </remarks>
    [HttpGet("series")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<string>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetSeries(
        CancellationToken cancellationToken)
    {
        var result = await matches.GetSeriesNamesAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(result));
    }

    /// <summary>
    /// Finished matches, reaching back past the provider's window into what we have kept.
    /// </summary>
    /// <param name="page">1-based. Values below 1 are treated as the first page.</param>
    /// <param name="pageSize">Clamped, because the caller does not get to decide how much we read.</param>
    [HttpGet("recent")]
    [ProducesResponseType<ApiResponse<PagedResult<MatchDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<MatchDto>>>> GetRecent(
        [FromQuery] MatchFilterQuery query,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultSize)
    {
        if (!query.TryToFilter(out var filter, out var error))
        {
            return BadRequest(ApiResponse<PagedResult<MatchDto>>.Fail(error!));
        }

        var requested = PageRequest.From(page, pageSize);

        var (result, total) = await matches.GetResultsAsync(filter, requested, cancellationToken);

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
