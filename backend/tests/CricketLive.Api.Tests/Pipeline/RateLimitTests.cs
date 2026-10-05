using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CricketLive.Api.Tests.Pipeline;

/// <summary>
/// Its own host, with a tiny allowance, so spending it cannot starve the other pipeline tests.
/// </summary>
public sealed class RateLimitTests : IDisposable
{
    private readonly ApiFactory factory = new() { RequestsPerMinute = 3 };

    [Fact]
    public async Task Past_the_limit_a_caller_gets_a_429_in_the_standard_envelope()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        }

        var refused = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.RetryAfter is not null, "A 429 should say when to come back.");

        var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Contains("Too many requests", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Crests_are_counted_in_their_own_bucket()
    {
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            await client.GetAsync("/api/health");
        }

        // The API allowance is spent; an image request must still be judged on its own limit.
        // A bogus token is a 404 from the crest route, which is the point: it got past the limiter.
        var crest = await client.GetAsync("/api/crests/not-a-token");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, crest.StatusCode);
    }

    public void Dispose() => factory.Dispose();
}
