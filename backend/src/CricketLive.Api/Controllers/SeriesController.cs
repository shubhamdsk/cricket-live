using CricketLive.Application.Common;
using CricketLive.Application.Series;
using CricketLive.Application.Series.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Series)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
public sealed class SeriesController(ISeriesService series) : ControllerBase
{
    /// <summary>
    /// Every series we hold a match for.
    /// </summary>
    /// <remarks>
    /// Assembled from those matches rather than fetched, so this costs no provider call beyond
    /// the current-matches window that every other list already shares.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SeriesDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SeriesDto>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var result = await series.GetAllAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<SeriesDto>>.Ok(result));
    }

    /// <param name="seriesId">Either the provider id or one of our slugs, which end in that id.</param>
    [HttpGet("{seriesId}")]
    [ProducesResponseType<ApiResponse<SeriesDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SeriesDetailsDto>>> GetById(
        string seriesId,
        CancellationToken cancellationToken)
    {
        var found = await series.GetByIdAsync(seriesId, cancellationToken);

        // A series we hold no match of cannot be told apart from one that never existed, so both
        // are 404 rather than an empty series that claims to exist.
        if (found is null)
        {
            return NotFound(ApiResponse<SeriesDetailsDto>.Fail("Series not found."));
        }

        return Ok(ApiResponse<SeriesDetailsDto>.Ok(found));
    }
}
