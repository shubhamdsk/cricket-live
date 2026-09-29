using System.Net;
using CricketLive.Application.Enrichment;
using CricketLive.Infrastructure.Cricbuzz;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Tests.Cricbuzz;

public class CricbuzzEnrichmentProviderTests
{
    /// <summary>Our id for the match, as CricketData numbers it.</summary>
    private const string MatchId = "90ae280c-cb10-4d58-9bcc-ec95294819e6";

    /// <summary>Cricbuzz's id for the same match, as somebody wrote it down in configuration.</summary>
    private const string CricbuzzId = "155422";

    /// <summary>
    /// These tests pin the fetching and caching behaviour, so they pair matches through the
    /// hand-written map and leave automatic resolution to <see cref="CricbuzzSlugTests"/>.
    /// </summary>
    private static MatchIdentity Identity(string matchId = MatchId) =>
        new(matchId, "2nd unofficial Test", "Australia A tour of India 2026", "INDA", "AUSA");

    private const string LivePage =
        """<html><head><meta property="og:title" content="AUSA 97/3 (32.4) (Jason Sangha 46(74) Nathan McSweeney 32(72)) | India A vs Australia A"></head></html>""";

    [Fact]
    public async Task Nothing_is_fetched_while_the_source_is_switched_off()
    {
        var harness = new Harness(enabled: false);

        var batters = await harness.Provider.GetCurrentBattersAsync(Identity(), default);

        Assert.Empty(batters);
        Assert.Equal(0, harness.Requests);
    }

    [Fact]
    public async Task An_enabled_source_returns_the_batters_on_the_page()
    {
        var harness = new Harness();

        var batters = await harness.Provider.GetCurrentBattersAsync(Identity(), default);

        Assert.Equal(
            [new BatterDto("Jason Sangha", 46, 74), new BatterDto("Nathan McSweeney", 32, 72)],
            batters);
    }

    [Fact]
    public async Task A_match_nobody_listed_is_never_fetched()
    {
        // The normal case. Almost no match will ever be mapped, and that is what keeps the load on
        // a site that never agreed to serve us bounded by hand rather than by traffic.
        var harness = new Harness();

        var batters = await harness.Provider.GetCurrentBattersAsync(Identity("some-other-match"), default);

        Assert.Empty(batters);
        Assert.Equal(0, harness.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("../../../etc/passwd")]
    [InlineData("155422/../../admin")]
    [InlineData("155422?x=1")]
    [InlineData("https://example.com/")]
    public async Task A_configured_source_id_that_is_not_a_plain_number_never_becomes_a_request(string configured)
    {
        // The configured value is interpolated into a URL, so a typo here is the one way a request
        // could be steered somewhere other than a scorecard.
        var harness = new Harness(map: new Dictionary<string, string> { [MatchId] = configured });

        var batters = await harness.Provider.GetCurrentBattersAsync(Identity(), default);

        Assert.Empty(batters);
        Assert.Equal(0, harness.Requests);
    }

    [Fact]
    public async Task A_second_viewer_of_the_same_match_costs_no_second_request()
    {
        var harness = new Harness();

        for (var i = 0; i < 25; i++)
        {
            await harness.Provider.GetCurrentBattersAsync(Identity(), default);
        }

        Assert.Equal(1, harness.Requests);
    }

    [Fact]
    public async Task A_match_with_nobody_batting_is_also_cached()
    {
        // Otherwise every viewer of a match between innings would reach the site individually,
        // which is the moment there are most viewers.
        var harness = new Harness(
            page: """<html><head><meta property="og:title" content="BAN vs MLY, 4th Quarter-Final"></head></html>""");

        Assert.Empty(await harness.Provider.GetCurrentBattersAsync(Identity(), default));
        Assert.Empty(await harness.Provider.GetCurrentBattersAsync(Identity(), default));
        Assert.Equal(1, harness.Requests);
    }

    [Fact]
    public async Task A_refusal_from_the_source_is_not_an_error_for_the_caller()
    {
        var harness = new Harness(status: HttpStatusCode.Forbidden);

        Assert.Empty(await harness.Provider.GetCurrentBattersAsync(Identity(), default));
    }

    [Fact]
    public async Task A_source_that_cannot_be_reached_is_not_an_error_for_the_caller()
    {
        // The match page must render whether or not this works, so a dead network reaches the
        // caller as two missing names and nothing else.
        var harness = new Harness(throws: new HttpRequestException("no route to host"));

        Assert.Empty(await harness.Provider.GetCurrentBattersAsync(Identity(), default));
    }

    [Fact]
    public async Task The_caller_giving_up_is_passed_on_rather_than_swallowed()
    {
        var harness = new Harness();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Provider.GetCurrentBattersAsync(Identity(), cancelled.Token));
    }

    [Fact]
    public async Task We_identify_ourselves_rather_than_posing_as_the_site()
    {
        var harness = new Harness();

        await harness.Provider.GetCurrentBattersAsync(Identity(), default);

        var sent = Assert.Single(harness.Sent);
        Assert.Contains("cricket-live", sent.Headers.UserAgent.ToString());

        // The project this was ported from sets both of these to cricbuzz.com so its traffic reads
        // as first-party. Sending them would be disguising the caller.
        Assert.Null(sent.Headers.Referrer);
        Assert.DoesNotContain(sent.Headers, header => header.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_request_goes_to_the_scorecard_for_that_match()
    {
        var harness = new Harness();

        await harness.Provider.GetCurrentBattersAsync(Identity(), default);

        Assert.Equal(
            "https://www.cricbuzz.com/live-cricket-scores/155422",
            Assert.Single(harness.Sent).RequestUri?.ToString());
    }

    private sealed class Harness
    {
        private readonly RecordingHandler _handler;

        public Harness(
            bool enabled = true,
            string page = LivePage,
            HttpStatusCode status = HttpStatusCode.OK,
            Exception? throws = null,
            IReadOnlyDictionary<string, string>? map = null)
        {
            _handler = new RecordingHandler(page, status, throws);

            var options = new CricbuzzOptions
            {
                Enabled = enabled,
                MatchIds = map ?? new Dictionary<string, string> { [MatchId] = CricbuzzId },

                // Off so a declined lookup cannot quietly become a second request and muddle the
                // counts these tests assert on. Resolution has its own tests.
                AutoResolve = false,
            };

            var client = new HttpClient(_handler)
            {
                BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + '/'),
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            var directory = new CricbuzzMatchDirectory(
                new HttpClient(new RecordingHandler("", HttpStatusCode.NotFound, null))
                {
                    BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + '/'),
                },
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(options),
                NullLogger<CricbuzzMatchDirectory>.Instance);

            Provider = new CricbuzzEnrichmentProvider(
                client,
                directory,
                new MemoryCache(new MemoryCacheOptions()),
                Options.Create(options),
                NullLogger<CricbuzzEnrichmentProvider>.Instance);
        }

        public IMatchEnrichmentProvider Provider { get; }

        public int Requests => _handler.Sent.Count;

        public IReadOnlyList<HttpRequestMessage> Sent => _handler.Sent;
    }

    private sealed class RecordingHandler(string page, HttpStatusCode status, Exception? throws)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Sent.Add(request);

            if (throws is not null)
            {
                return Task.FromException<HttpResponseMessage>(throws);
            }

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(page),
            });
        }
    }
}
