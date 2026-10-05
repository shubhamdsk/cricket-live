using CricketLive.Api.Contracts;
using CricketLive.Api.Controllers;
using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Scorecards.Dtos;
using CricketLive.Application.Teams;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Tests;

public sealed class MatchesControllerTests
{
    private readonly WindowFreshness freshness = new();

    [Fact]
    public async Task GetLive_returns_Ok_without_AsOfUtc_when_data_is_current()
    {
        var match = MatchDto("live-1", MatchStatus.Live);
        var service = new StubMatchService { LiveMatches = [match] };
        var controller = new MatchesController(service, freshness);

        var actionResult = await controller.GetLive(new MatchFilterQuery(), default);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MatchDto>>>(okResult.Value);

        Assert.True(response.Success);
        Assert.Null(response.AsOfUtc);
        Assert.Single(response.Data!);
        Assert.Equal("live-1", response.Data![0].Id);
    }

    [Fact]
    public async Task GetLive_returns_Stale_with_AsOfUtc_when_freshness_is_marked()
    {
        var capturedAt = new DateTimeOffset(2026, 10, 2, 8, 30, 0, TimeSpan.Zero);
        freshness.MarkRecovered(capturedAt);

        var match = MatchDto("live-1", MatchStatus.Live);
        var service = new StubMatchService { LiveMatches = [match] };
        var controller = new MatchesController(service, freshness);

        var actionResult = await controller.GetLive(new MatchFilterQuery(), default);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MatchDto>>>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal(capturedAt, response.AsOfUtc);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetUpcoming_returns_Stale_with_AsOfUtc_when_freshness_is_marked()
    {
        var capturedAt = new DateTimeOffset(2026, 10, 2, 8, 30, 0, TimeSpan.Zero);
        freshness.MarkRecovered(capturedAt);

        var match = MatchDto("up-1", MatchStatus.Upcoming);
        var service = new StubMatchService { UpcomingMatches = [match] };
        var controller = new MatchesController(service, freshness);

        var actionResult = await controller.GetUpcoming(new MatchFilterQuery(), default);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<IReadOnlyList<MatchDto>>>(okResult.Value);

        Assert.True(response.Success);
        Assert.Equal(capturedAt, response.AsOfUtc);
        Assert.Single(response.Data!);
        Assert.Equal("up-1", response.Data![0].Id);
    }

    private static MatchDto MatchDto(string id, MatchStatus status)
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

        return new MatchDto
        {
            Id = id,
            Slug = id,
            Status = status,
            Format = MatchFormat.T20,
            SeriesId = "series-a",
            SeriesName = "A Series",
            MatchTitle = "1st T20I",
            Venue = "Wankhede Stadium",
            StartTimeUtc = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero),
            Home = team,
            Away = team,
            StatusText = string.Empty,
        };
    }

    private sealed class StubMatchService : IMatchService
    {
        public IReadOnlyList<MatchDto> LiveMatches { get; init; } = [];
        public IReadOnlyList<MatchDto> UpcomingMatches { get; init; } = [];

        public Task<IReadOnlyList<MatchDto>> GetLiveAsync(MatchFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(LiveMatches);

        public Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(MatchFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(UpcomingMatches);

        public Task<(IReadOnlyList<MatchDto> Matches, int Total)> GetResultsAsync(
            MatchFilter filter,
            PageRequest page,
            CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<MatchDto>, int)>(([], 0));

        public Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult<MatchDetailsDto?>(null);

        public Task<ScorecardDto?> GetScorecardAsync(string matchId, CancellationToken cancellationToken)
            => Task.FromResult<ScorecardDto?>(null);

        public Task<IReadOnlyList<string>> GetSeriesNamesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
