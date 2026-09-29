using CricketLive.Application.Common;
using CricketLive.Application.Search;
using CricketLive.Application.Search.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Search)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
public sealed class SearchController(ISearchService search) : ControllerBase
{
    /// <summary>
    /// Matches, teams and series whose names contain the query.
    /// </summary>
    /// <remarks>
    /// A query shorter than two characters returns an empty result rather than an error: a caller
    /// typing into a box is not making a mistake, and 400 would make the UI report one.
    /// </remarks>
    /// <param name="q">The search term.</param>
    [HttpGet]
    [ProducesResponseType<ApiResponse<SearchResultsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SearchResultsDto>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var results = await search.SearchAsync(q ?? string.Empty, cancellationToken);

        return Ok(ApiResponse<SearchResultsDto>.Ok(results));
    }
}
