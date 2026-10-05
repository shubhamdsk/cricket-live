using CricketLive.Application.Live;
using CricketLive.Application.Matches;
using CricketLive.Application.Search;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;
using CricketLive.Infrastructure.CricketData;
using CricketLive.Infrastructure.Health;
using CricketLive.Infrastructure.Live;
using CricketLive.Infrastructure.Persistence;
using CricketLive.Infrastructure.Scope;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure;

public static partial class DependencyInjection
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
        // Three decorators, and the order is the decision rather than an accident of writing them.
        //
        // Archiving is innermost, so it keeps every finished match the provider sends, including
        // ones the site does not cover. Reversed, the archive would hold only what today's policy
        // allows and widening it later would mean re-earning history three provider pages at a day.
        //
        // Snapshotting is next, so the window it stores is what a caller would have been given —
        // crests already rewritten, scores already mapped — and so it is still the provider's whole
        // window rather than our filtered view of it.
        //
        // The scope filter is outermost, so callers only ever see cricket we cover, whether the
        // window came from the provider a second ago or from the snapshot after an outage.
        services.AddScoped<CricketDataProvider>();
        services.AddScoped<ICricketDataProvider>(provider => new ScopedCricketDataProvider(
            new SnapshottingCricketDataProvider(
                new ArchivingCricketDataProvider(
                    provider.GetRequiredService<CricketDataProvider>(),
                    provider.GetRequiredService<IMatchArchive>(),
                    provider.GetRequiredService<ILogger<ArchivingCricketDataProvider>>()),
                provider.GetRequiredService<IWindowSnapshotStore>(),
                provider.GetRequiredService<WindowFreshness>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<SnapshottingCricketDataProvider>>()),
            provider.GetRequiredService<ILogger<ScopedCricketDataProvider>>()));

        // Scoped, because it is a note about one request. A singleton would have one request's
        // outage labelling the next request's fresh data.
        services.AddScoped<WindowFreshness>();

        // Not behind the archiving decorator. The two series sources return series rather than the
        // live window, so the archive has no opinion about them; the match index returns matches
        // too thin to archive — no venue, no title, and a team written "India [IND]". All three
        // share the budget-claiming client.
        services.AddScoped<CricketDataSeriesIndex>();
        services.AddScoped<CricketDataSeriesFixtures>();
        services.AddScoped<CricketDataMatchIndex>();

        services.AddScoped<IPendingMatches, PendingMatches>();

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

        // Second hosted service, and the only thing here that fetches history rather than keeping
        // what it was handed. It holds itself to half the daily allowance, so the poller above —
        // which is serving somebody who is watching — always outranks it.
        services.AddHostedService<CricketDataMatchBackfill>();

        AddCricbuzzEnrichment(services, configuration);
        AddScorecards(services, configuration);
        AddStandings(services);
        AddSeriesIndex(services);
        AddSeriesFixtures(services);
        AddMatchIndex(services);
        AddHealth(services);

        return services;
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
}
