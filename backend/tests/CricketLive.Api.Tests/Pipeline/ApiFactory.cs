using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CricketLive.Api.Tests.Pipeline;

/// <summary>
/// The real application, middleware and all, with nothing that can reach the outside world.
/// </summary>
/// <remarks>
/// Settings go in through <c>UseSetting</c> because <c>Program</c> reads the CORS list and the
/// rate limits from configuration before the app is built, and that is the only route in that
/// early. The pollers are removed so a test never races a background provider call, and the
/// provider's handler counts what reaches it so a test can say "this cost nothing".
/// </remarks>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://site.test";

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"cricket-live-{Guid.NewGuid():N}.db");

    public int RequestsPerMinute { get; init; } = 1000;

    public CountingHandler Provider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("CricketData:ApiKey", "test-key-not-real");
        builder.UseSetting("ConnectionStrings:Archive", $"Data Source={databasePath}");
        builder.UseSetting("RateLimits:RequestsPerMinute", RequestsPerMinute.ToString());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            // Every outbound client, not only the provider's: crests and Cricbuzz must not reach
            // the internet from a test either, and counting them all is the stricter claim.
            services.ConfigureHttpClientDefaults(client =>
                client.ConfigurePrimaryHttpMessageHandler(() => Provider));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    public sealed class CountingHandler : HttpMessageHandler
    {
        private int calls;

        public int Calls => calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
