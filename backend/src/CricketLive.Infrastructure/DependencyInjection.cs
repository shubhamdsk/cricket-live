using CricketLive.Application.Enrichment;
using CricketLive.Application.Live;
using CricketLive.Application.Matches;
using CricketLive.Application.Scorecards;
using CricketLive.Application.Search;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;
using CricketLive.Infrastructure.Cricbuzz;
using CricketLive.Infrastructure.CricbuzzApi;
using CricketLive.Infrastructure.CricketData;
using CricketLive.Infrastructure.Health;
using CricketLive.Infrastructure.Live;
using CricketLive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

        AddArchive(services, configuration);

        // Registered as the decorator, so nothing that asks for cricket data has to know that
        // finished matches are being kept on the way past.
        services.AddScoped<CricketDataProvider>();
        services.AddScoped<ICricketDataProvider>(provider => new ArchivingCricketDataProvider(
            provider.GetRequiredService<CricketDataProvider>(),
            provider.GetRequiredService<IMatchArchive>(),
            provider.GetRequiredService<ILogger<ArchivingCricketDataProvider>>()));

        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<ISeriesService, SeriesService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ISearchService, SearchService>();

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
        AddScorecards(services, configuration);
        AddStandings(services);
        AddHealth(services);

        return services;
    }

    /// <summary>
    /// Registers the store that keeps finished matches after the provider's window moves on.
    /// </summary>
    /// <remarks>
    /// SQLite because it is a file. There is nothing to install, nothing to run alongside the API
    /// and nothing to provision, and a few thousand finished matches is not a workload that needs
    /// more. The abstraction is <see cref="IMatchArchive"/>, so swapping the provider at deployment
    /// changes this method and nothing else.
    /// </remarks>
    private static void AddArchive(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Archive")
            ?? "Data Source=cricket-live.db";

        services.AddDbContext<CricketLiveDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IMatchArchive, SqlMatchArchive>();
    }

    /// <summary>
    /// The two things worth knowing about this service's dependencies.
    /// </summary>
    /// <remarks>
    /// Registered here rather than in the API project because both checks read types that are
    /// internal to this assembly — the <c>DbContext</c> and the call budget — and exposing either
    /// one publicly to satisfy a health endpoint would be the wrong trade.
    /// </remarks>
    private static void AddHealth(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<ArchiveHealthCheck>("archive", tags: ["ready"])
            .AddCheck<ProviderBudgetHealthCheck>("provider-budget", tags: ["ready"]);
    }

    /// <summary>
    /// Brings the archive's schema up to date, creating the file if it is not there yet.
    /// </summary>
    /// <remarks>
    /// Migrating on startup suits a single instance writing to a local file and would not suit a
    /// cluster, where two instances racing the same migration is a real failure. That is a
    /// deployment-time change, and it belongs with the change of provider.
    /// </remarks>
    public static async Task MigrateArchiveAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<CricketLiveDbContext>()
            .Database.MigrateAsync(cancellationToken);
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

    /// <summary>
    /// Registers the scorecard source, which reaches a metered gateway and so ships switched off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Conditional rather than dormant, like the standings reader and unlike enrichment. The
    /// difference is that this one spends somebody's allowance: a registration that is merely
    /// inert puts a single configuration flag between a deployment and a metered API. With the
    /// switch off the client is not in the graph at all.
    /// </para>
    /// <para>
    /// The budget is a singleton because it is the process's count of a monthly allowance, and two
    /// of them would each believe they had the whole thing.
    /// </para>
    /// </remarks>
    private static void AddScorecards(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CricbuzzApiOptions>()
            .Bind(configuration.GetSection(CricbuzzApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<NoMatchScorecardProvider>();
        services.AddSingleton<CricbuzzApiBudget>();

        services.AddHttpClient<CricbuzzApiClient>((provider, client) =>
        {
            var api = provider.GetRequiredService<IOptions<CricbuzzApiOptions>>().Value;

            client.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + '/');
            client.Timeout = TimeSpan.FromSeconds(api.TimeoutSeconds);

            // A header rather than a query string, which is the one thing this gateway does better
            // than our main provider: the key never appears in a URL, so nothing that logs a URL
            // can leak it.
            client.DefaultRequestHeaders.Add("x-rapidapi-key", api.ApiKey);
            client.DefaultRequestHeaders.Add("x-rapidapi-host", api.Host);
        });

        // Deliberately no resilience handler. Our main provider gets one because a hundred calls a
        // day can absorb a retry; two hundred a month cannot, and a retry here would spend a
        // second call re-asking a scraper that had just failed.
        services.AddScoped<CricbuzzApiScorecardProvider>();

        services.AddScoped<IMatchScorecardProvider>(provider =>
            provider.GetRequiredService<IOptions<CricbuzzApiOptions>>().Value.Enabled
                ? provider.GetRequiredService<CricbuzzApiScorecardProvider>()
                : provider.GetRequiredService<NoMatchScorecardProvider>());
    }

    /// <summary>
    /// Registers whatever can supply a points table, which by default is nothing.
    /// </summary>
    /// <remarks>
    /// The only source we found publishes one behind a <c>robots.txt</c> that disallows us, so
    /// unlike the other registrations here this one is conditional: when the switch is off, the
    /// Cricbuzz reader is not in the graph at all rather than present and dormant. Configuration
    /// should not be the only thing standing between a deployment and traffic it did not intend
    /// to send.
    /// </remarks>
    private static void AddStandings(IServiceCollection services)
    {
        services.AddSingleton<NoSeriesStandingsProvider>();

        services.AddHttpClient<CricbuzzStandingsProvider>((provider, client) =>
        {
            var cricbuzz = provider.GetRequiredService<IOptions<CricbuzzOptions>>().Value;

            client.BaseAddress = new Uri(cricbuzz.BaseUrl.TrimEnd('/') + '/');
            client.Timeout = TimeSpan.FromSeconds(cricbuzz.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(cricbuzz.UserAgent);
        });

        services.AddScoped<ISeriesStandingsProvider>(provider =>
            provider.GetRequiredService<IOptions<CricbuzzOptions>>().Value.StandingsEnabled
                ? provider.GetRequiredService<CricbuzzStandingsProvider>()
                : provider.GetRequiredService<NoSeriesStandingsProvider>());
    }
}
