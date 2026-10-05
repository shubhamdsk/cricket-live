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
using CricketLive.Infrastructure.Scope;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

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
    /// Registers the store that keeps finished matches after the provider's window moves on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two providers, chosen by the shape of the connection string rather than by a second
    /// setting that could disagree with it. SQLite for local runs, because it is a file and there
    /// is nothing to install; PostgreSQL for deployments, because the free hosts worth using will
    /// not keep a file and the archive is the one thing here that cannot be rebuilt. D-028.
    /// </para>
    /// <para>
    /// Inferring the provider is a small piece of cleverness and it is deliberate: a
    /// <c>Database:Provider</c> setting alongside a connection string is two facts that can
    /// contradict each other, and the failure would be at startup in production.
    /// </para>
    /// </remarks>
    private static void AddArchive(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Archive")
            ?? "Data Source=cricket-live.db";

        services.AddDbContext<CricketLiveDbContext>(options =>
        {
            if (IsSqlite(connection))
            {
                options.UseSqlite(connection);
            }
            else
            {
                // Retry, because a free-tier PostgreSQL host suspends its compute after a few
                // minutes idle and this site is idle most of the time. The pool hands out a
                // connection the server has already dropped, the first query fails, and the
                // second — after a wake that takes a moment — succeeds. Without this, the first
                // visitor after a quiet spell gets an error and everyone after them is fine,
                // which is the most annoying shape a bug can have.
                options.UseNpgsql(ToNpgsqlConnectionString(connection), npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null));
            }
        });

        services.AddScoped<IMatchArchive, SqlMatchArchive>();
        services.AddScoped<IWindowSnapshotStore, SqlWindowSnapshotStore>();
    }

    /// <summary>
    /// Whether this connection string is SQLite's.
    /// </summary>
    /// <remarks>
    /// SQLite's only required keyword is <c>Data Source</c>, and while PostgreSQL accepts that as
    /// a synonym for <c>Host</c> in theory, no host hands one out written that way: they are URIs
    /// or they name <c>Host=</c>. Checking for the URI scheme first means a Neon or Render
    /// connection string is never mistaken for a file path.
    /// </remarks>
    private static bool IsSqlite(string connection)
        => !connection.StartsWith("postgres", StringComparison.OrdinalIgnoreCase)
           && connection.Contains("Data Source", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Accepts either the URL a PostgreSQL host hands out or the key-value form Npgsql wants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every managed host — Neon, Render, Supabase — gives you a <c>postgresql://</c> URL, and
    /// every one-click integration pastes that URL into the environment for you. Npgsql does not
    /// parse it. It throws, and the exception text contains the whole connection string, so the
    /// first thing a failed deploy does is print the database password into a log.
    /// </para>
    /// <para>
    /// Translating here means the value from the host works unmodified, which is worth more than
    /// it looks: the alternative is a documented hand-conversion, done under deploy pressure, on
    /// a string containing a password.
    /// </para>
    /// </remarks>
    private static string ToNpgsqlConnectionString(string connection)
    {
        if (!connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connection;
        }

        var url = new Uri(connection);
        var credentials = url.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.IsDefaultPort ? 5432 : url.Port,
            Database = url.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credentials[0]),

            // Required rather than merely preferred, and not negotiable down: every host that
            // issues a URL is reached over the public internet.
            SslMode = SslMode.Require,
        };

        if (credentials.Length == 2)
        {
            builder.Password = Uri.UnescapeDataString(credentials[1]);
        }

        // The query string is deliberately not carried over. What hosts put there are libpq's
        // options, and the two Neon sends are already covered: sslmode is set above, and channel
        // binding is something Npgsql negotiates on its own. Copying them across blindly would
        // fail, because libpq spells them with underscores and Npgsql does not.
        return builder.ConnectionString;
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
    /// Brings the archive's schema up to date, creating it if it is not there yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Migrating on startup suits a single instance and would not suit a cluster, where two
    /// instances racing the same migration is a real failure. The deployment is pinned to one
    /// instance for other reasons already — SQLite is gone but the provider call budget is still
    /// counted per process — so this holds for now and is listed in docs/deployment.md as
    /// something that stops holding the moment a second instance exists.
    /// </para>
    /// <para>
    /// The migrations are PostgreSQL's, because that is what deployments run and migrations are
    /// provider-specific SQL. A local SQLite file is built from the model instead. That is a real
    /// asymmetry and the honest reason for it is that keeping two migration sets means two
    /// projects, which is a lot of machinery for a scratch database that is deleted whenever it
    /// is inconvenient. The cost is that a model change which breaks a migration will not show up
    /// locally — it shows up on deploy. D-028.
    /// </para>
    /// </remarks>
    public static async Task MigrateArchiveAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CricketLiveDbContext>();

        if (context.Database.IsSqlite())
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        // After the schema and before the first request, because the scope flag decides what every
        // list shows and a stale one would serve cricket we do not cover. See ArchiveScopeRefresh
        // for why this runs every start rather than once.
        await ArchiveScopeRefresh.ApplyAsync(
            context,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<ArchiveScopeRefresh>(),
            cancellationToken);
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

    /// <summary>
    /// The series index, or nothing when its page budget is set to zero.
    /// </summary>
    /// <remarks>
    /// Zero pages resolves to <see cref="NoSeriesIndex"/> rather than to a reader that loops no
    /// times, so turning it off is one decision made in one place instead of a configured value
    /// that happens to be harmless. The series list then falls back to held matches, which is
    /// where it started.
    /// </remarks>
    private static void AddSeriesIndex(IServiceCollection services)
    {
        services.AddSingleton<NoSeriesIndex>();

        services.AddScoped<ISeriesIndex>(provider => new ScopedSeriesIndex(
            provider.GetRequiredService<IOptions<CricketDataOptions>>().Value.SeriesIndexPages > 0
                ? provider.GetRequiredService<CricketDataSeriesIndex>()
                : provider.GetRequiredService<NoSeriesIndex>(),
            provider.GetRequiredService<ILogger<ScopedSeriesIndex>>()));
    }

    /// <summary>
    /// Per-series fixture lists, or nothing when their cache lifetime is set to zero.
    /// </summary>
    /// <remarks>
    /// Zero hours resolves to <see cref="NoSeriesFixtures"/> rather than to a cache that expires
    /// immediately, which would read the provider on every page view and spend the day's allowance
    /// in an afternoon. Off is a decision; a zero lifetime would be an accident.
    /// </remarks>
    private static void AddSeriesFixtures(IServiceCollection services)
    {
        services.AddSingleton<NoSeriesFixtures>();

        services.AddScoped<ISeriesFixtures>(provider => new ScopedSeriesFixtures(
            provider.GetRequiredService<IOptions<CricketDataOptions>>().Value.SeriesFixturesCacheHours > 0
                ? provider.GetRequiredService<CricketDataSeriesFixtures>()
                : provider.GetRequiredService<NoSeriesFixtures>(),
            provider.GetRequiredService<ILogger<ScopedSeriesFixtures>>()));
    }

    /// <summary>
    /// The wider view of what is on around now, or nothing when its cache lifetime is set to zero.
    /// </summary>
    /// <remarks>
    /// Switching this off leaves the live and upcoming lists reading the main window alone, which
    /// is how they behaved before and is the reason the upcoming list was empty. Same shape as the
    /// two series sources, for the same reason: off is a decision, a zero lifetime is an accident.
    /// </remarks>
    private static void AddMatchIndex(IServiceCollection services)
    {
        services.AddSingleton<NoMatchIndex>();

        services.AddScoped<IMatchIndex>(provider => new ScopedMatchIndex(
            provider.GetRequiredService<IOptions<CricketDataOptions>>().Value.MatchIndexCacheMinutes > 0
                ? provider.GetRequiredService<CricketDataMatchIndex>()
                : provider.GetRequiredService<NoMatchIndex>(),
            provider.GetRequiredService<ILogger<ScopedMatchIndex>>()));
    }
}
