using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches;
using CricketLive.Application.Scorecards;
using CricketLive.Application.Series;
using CricketLive.Infrastructure.Cricbuzz;
using CricketLive.Infrastructure.CricbuzzApi;
using CricketLive.Infrastructure.CricketData;
using CricketLive.Infrastructure.Scope;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure;

public static partial class DependencyInjection
{
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
