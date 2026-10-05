using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CricketLive.Api.Tests.Pipeline;

/// <summary>
/// What a caller actually receives, through every piece of middleware rather than a controller
/// called by hand. The controller tests next door cannot see headers, limits or CORS at all.
/// </summary>
public sealed class PipelineTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;

    public PipelineTests(ApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Every_response_carries_the_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Contains("default-src 'none'", Header(response, "Content-Security-Policy"));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task An_error_response_carries_them_too()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    [Fact]
    public async Task Liveness_answers_without_touching_the_provider()
    {
        var before = factory.Provider.Calls;

        var response = await factory.CreateClient().GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, factory.Provider.Calls);
    }

    [Theory]
    [InlineData("not-a-match")]
    [InlineData("india-vs-west-indies")]
    [InlineData("12345")]
    public async Task A_malformed_match_id_is_a_404_that_costs_no_provider_call(string matchId)
    {
        var before = factory.Provider.Calls;

        var response = await factory.CreateClient().GetAsync($"/api/matches/{matchId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal("Match not found.", body.GetProperty("message").GetString());
        Assert.Equal(before, factory.Provider.Calls);
    }

    [Fact]
    public async Task The_allowed_origin_passes_a_preflight()
    {
        var response = await Preflight(ApiFactory.AllowedOrigin);

        Assert.Equal(ApiFactory.AllowedOrigin, Header(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Any_other_origin_is_refused_a_preflight()
    {
        var response = await Preflight("https://evil.test");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private Task<HttpResponseMessage> Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/matches/live");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return factory.CreateClient().SendAsync(request);
    }

    private static string Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values)
            || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : string.Empty;
}
