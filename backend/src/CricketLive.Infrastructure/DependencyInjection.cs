using CricketLive.Application.Enrichment;
using CricketLive.Application.Live;
using CricketLive.Application.Matches;
using CricketLive.Infrastructure.Cricbuzz;
using CricketLive.Infrastructure.CricketData;
using CricketLive.Infrastructure.Live;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Read once here as well, because the resilience handler is configured at registration time
        // and has no access to the service provider.
        var settings = configuration
            .GetSection(CricketDataOptions.SectionName)
            .Get<CricketDataOptions>() ?? new CricketDataOptions();

        services
            .AddOptions<CricketDataOptions>()
            .Bind(configuration.GetSection(CricketDataOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.AddSingleton<CricketDataHitBudget>();
        services.AddSingleton<CricketDataMatchMapper>();

        services.AddHttpClient<CricketDataClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CricketDataOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + '/');

            // The resilience pipeline below owns every timeout, so the handler must not impose its own.
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddStandardResilienceHandler(resilience =>
        {
            var attempt = TimeSpan.FromSeconds(settings.TimeoutSeconds);

            // Every attempt costs one of a hundred daily calls, so we retry once and no more.
            resilience.Retry.MaxRetryAttempts = 1;
            resilience.Retry.Delay = TimeSpan.FromSeconds(1);

            // Fail fast while the provider is down rather than spending the day's allowance on it.
            resilience.CircuitBreaker.MinimumThroughput = 2;
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);
            resilience.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);

            resilience.AttemptTimeout.Timeout = attempt;
            resilience.TotalRequestTimeout.Timeout = (attempt * 2) + TimeSpan.FromSeconds(2);
        });

        services.AddScoped<ICricketDataProvider, CricketDataProvider>();
        services.AddScoped<IMatchService, MatchService>();

        services
            .AddOptions<LiveOptions>()
            .Bind(configuration.GetSection(LiveOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Singleton because it is the process's list of connected clients; the poller and every
        // request handler must see the same one.
        services.AddSingleton<IMatchBroadcaster, MatchBroadcaster>();
        services.AddHostedService<LiveMatchPoller>();

        AddCricbuzzEnrichment(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers the supplementary batter source, which stays dormant until switched on.
    /// </summary>
    /// <remarks>
    /// Registered unconditionally so the shape of the graph does not change with configuration, and
    /// so a match page behaves the same either way. The provider itself returns nothing while
    /// disabled, which costs one boolean and saves a nullable dependency everywhere it is used.
    /// </remarks>
    private static void AddCricbuzzEnrichment(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CricbuzzOptions>()
            .Bind(configuration.GetSection(CricbuzzOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IMatchEnrichmentProvider, CricbuzzEnrichmentProvider>(Configure);

        // Its own client so the listing and the scorecards do not share a connection budget, and
        // so a slow listing cannot time out a scorecard that was already in flight.
        services.AddHttpClient<CricbuzzMatchDirectory>(Configure);

        static void Configure(IServiceProvider provider, HttpClient client)
        {
            var cricbuzz = provider.GetRequiredService<IOptions<CricbuzzOptions>>().Value;

            client.BaseAddress = new Uri(cricbuzz.BaseUrl.TrimEnd('/') + '/');
            client.Timeout = TimeSpan.FromSeconds(cricbuzz.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(cricbuzz.UserAgent);
        }

        // Deliberately no retry. This reads someone else's website, and a page that did not answer
        // is not an invitation to ask again — the caller loses two player names, which is nothing.
    }
}
