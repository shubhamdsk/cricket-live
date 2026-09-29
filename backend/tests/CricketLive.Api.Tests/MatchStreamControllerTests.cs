using System.Text;
using System.Threading.Channels;
using CricketLive.Api.Controllers;
using CricketLive.Application.Live;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.Live;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CricketLive.Api.Tests;

public sealed class MatchStreamControllerTests
{
    private const string MatchId = "90ae280c-cb10-4d58-9bcc-ec95294819e6";

    [Fact]
    public async Task An_unknown_match_is_a_404_and_never_becomes_a_stream()
    {
        var harness = new Harness(match: null);

        await harness.Controller.Stream("nonsense", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, harness.Context.Response.StatusCode);
        Assert.Equal(string.Empty, harness.Body);
    }

    [Fact]
    public async Task The_current_score_arrives_on_connect_so_a_client_is_never_blank()
    {
        var harness = new Harness(Match(runs: 300));
        using var stopping = new CancellationTokenSource();

        var streaming = harness.Controller.Stream(MatchId, stopping.Token);
        await harness.WaitForBodyToContain("event: match");

        await stopping.CancelAsync();
        await streaming;

        Assert.Equal("text/event-stream", harness.Context.Response.ContentType);
        Assert.Contains("\"runs\":300", harness.Body);
    }

    [Fact]
    public async Task A_finished_match_is_told_to_close_rather_than_held_open()
    {
        var harness = new Harness(Match(status: MatchStatus.Completed));

        // No cancellation: if the controller failed to end the stream itself this would hang.
        await harness.Controller.Stream(MatchId, CancellationToken.None);

        Assert.Contains("event: end", harness.Body);
        Assert.Contains("\"reason\":\"completed\"", harness.Body);
    }

    [Fact]
    public async Task A_published_update_reaches_the_connected_client()
    {
        var harness = new Harness(Match(runs: 300));
        using var stopping = new CancellationTokenSource();

        var streaming = harness.Controller.Stream(MatchId, stopping.Token);

        await harness.WaitForBodyToContain("\"runs\":300");
        harness.Broadcaster.Publish([Match(runs: 314)]);
        await harness.WaitForBodyToContain("\"runs\":314");

        await stopping.CancelAsync();
        await streaming;

        Assert.Contains("\"runs\":314", harness.Body);
    }

    [Fact]
    public async Task An_update_that_finishes_the_match_closes_the_stream()
    {
        var harness = new Harness(Match(runs: 300));

        var streaming = harness.Controller.Stream(MatchId, CancellationToken.None);

        await harness.WaitForBodyToContain("\"runs\":300");
        harness.Broadcaster.Publish([Match(status: MatchStatus.Completed)]);

        // Completes on its own: an unfinished stream would leave this task pending forever.
        await streaming.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains("event: end", harness.Body);
    }

    /// <summary>
    /// A silent connection has to keep speaking or the proxy in front of it closes the response.
    /// Task 8.26 calls this the most likely deployment failure, so it is worth pinning down.
    /// </summary>
    [Fact]
    public async Task A_silent_connection_sends_a_heartbeat()
    {
        var harness = new Harness(Match(runs: 300));
        using var stopping = new CancellationTokenSource();

        var streaming = harness.Controller.Stream(MatchId, stopping.Token);
        await harness.WaitForBodyToContain("\"runs\":300");

        // Nothing is published; only the clock moves.
        harness.Clock.Advance(TimeSpan.FromSeconds(25));
        await harness.WaitForBodyToContain(": keepalive");

        await stopping.CancelAsync();
        await streaming;

        Assert.Contains(": keepalive", harness.Body);
    }

    private sealed class Harness
    {
        public Harness(MatchDetailsDto? match)
        {
            Context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
            Broadcaster = new TestBroadcaster();

            Controller = new MatchStreamController(
                new StubMatchService(match),
                Broadcaster,
                Options.Create(new LiveOptions { HeartbeatSeconds = 20 }),
                Clock,
                NullLogger<MatchStreamController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = Context },
            };
        }

        public DefaultHttpContext Context { get; }

        public TestBroadcaster Broadcaster { get; }

        public FakeTimeProvider Clock { get; } = new();

        public MatchStreamController Controller { get; }

        public string Body => Encoding.UTF8.GetString(((MemoryStream)Context.Response.Body).ToArray());

        public async Task WaitForBodyToContain(string fragment)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < deadline)
            {
                if (Body.Contains(fragment, StringComparison.Ordinal))
                {
                    return;
                }

                await Task.Delay(10);
            }

            Assert.Fail($"The stream never wrote '{fragment}'. Body was:\n{Body}");
        }
    }

    /// <summary>
    /// A stand-in rather than the real <c>MatchBroadcaster</c>, which is internal to Infrastructure
    /// and should stay that way — the API only ever sees the interface. The real fan-out has its own
    /// tests next to it; what these tests are about is what the controller writes down the wire.
    /// </summary>
    private sealed class TestBroadcaster : IMatchBroadcaster
    {
        private readonly List<(string MatchId, ChannelWriter<MatchDetailsDto> Writer)> _writers = [];

        public bool HasSubscribers
        {
            get
            {
                lock (_writers)
                {
                    return _writers.Count > 0;
                }
            }
        }

        public MatchSubscription Subscribe(string matchId)
        {
            var channel = Channel.CreateBounded<MatchDetailsDto>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
            });

            var entry = (matchId, channel.Writer);

            lock (_writers)
            {
                _writers.Add(entry);
            }

            return new MatchSubscription(channel.Reader, () =>
            {
                lock (_writers)
                {
                    _writers.Remove(entry);
                }
            });
        }

        public void Publish(IReadOnlyList<MatchDetailsDto> matches)
        {
            lock (_writers)
            {
                foreach (var match in matches)
                {
                    foreach (var (matchId, writer) in _writers)
                    {
                        if (string.Equals(matchId, match.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            writer.TryWrite(match);
                        }
                    }
                }
            }
        }
    }

    private sealed class StubMatchService(MatchDetailsDto? match) : IMatchService
    {
        public Task<IReadOnlyList<MatchDto>> GetLiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<IReadOnlyList<MatchDto>> GetRecentAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MatchDto>>([]);

        public Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken) =>
            Task.FromResult(match);
    }

    private static MatchDetailsDto Match(int runs = 300, MatchStatus status = MatchStatus.Live) => new()
    {
        Id = MatchId,
        Slug = $"india-vs-west-indies-{MatchId}",
        Status = status,
        Format = MatchFormat.Odi,
        SeriesName = "West Indies tour of India, 2026",
        MatchTitle = "1st ODI",
        Venue = "Greenfield International Stadium",
        StartTimeUtc = DateTimeOffset.Parse("2026-09-27T08:30:00Z"),
        Home = new TeamInningsDto(
            new TeamDto("india", "India", "IND", null),
            [new InningsScoreDto(1, runs, 2, "41.4")]),
        Away = new TeamInningsDto(
            new TeamDto("west-indies", "West Indies", "WI", null),
            [new InningsScoreDto(1, 295, 7, "50")]),
        StatusText = "India need 5 runs",
        HasBallByBall = false,
        HasSquads = false,
    };
}
