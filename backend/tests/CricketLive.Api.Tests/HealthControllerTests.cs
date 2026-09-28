using CricketLive.Api.Contracts;
using CricketLive.Api.Controllers;
using CricketLive.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace CricketLive.Api.Tests;

public sealed class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsHealthyInsideSuccessEnvelope()
    {
        var controller = new HealthController(new StubHostEnvironment("Testing"));

        var result = controller.Get().Result as OkObjectResult;
        var response = Assert.IsType<ApiResponse<HealthStatusDto>>(result?.Value);

        Assert.True(response.Success);
        Assert.Equal("Healthy", response.Data?.Status);
        Assert.Equal("Testing", response.Data?.Environment);
        Assert.Null(response.Errors);
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CricketLive.Api.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
