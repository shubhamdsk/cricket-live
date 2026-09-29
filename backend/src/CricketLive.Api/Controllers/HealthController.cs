using CricketLive.Api.Contracts;
using CricketLive.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Health)]
public sealed class HealthController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<HealthStatusDto>>(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthStatusDto>> Get()
    {
        var status = new HealthStatusDto(
            Status: "Healthy",
            Environment: environment.EnvironmentName,
            TimestampUtc: DateTimeOffset.UtcNow);

        return Ok(ApiResponse<HealthStatusDto>.Ok(status));
    }
}
