using CricketLive.Application.Enrichment;
using CricketLive.Application.Live;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.CricketData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Live;

/// <summary>
/// The only thing in the system that asks the provider for cricket on a schedule.
/// </summary>
/// <remarks>
/// Three rules keep a hundred calls a day survivable, and all three matter:
/// <list type="number">
/// <item>It does not poll at all unless somebody is watching, so idle days cost nothing.</item>
/// <item>Its tick rate is not its spend rate. Every tick goes through the same five-minute cache
/// the HTTP endpoints use, so ticking often shortens the gap between a refresh and the push that
/// follows it without buying another call.</item>
/// <item>It stops once only the reserve is left, so a tab left open all day degrades itself rather
/// than starving someone opening a match page.</item>
/// </list>
/// </remarks>
internal sealed class LiveMatchPoller(
    IServiceScopeFactory scopes,
    IMatchBroadcaster broadcaster,
    CricketDataHitBudget budget,
    IOptions<LiveOptions> options,
    TimeProvider timeProvider,
    ILogger<LiveMatchPoller> logger) : BackgroundService
{
    /// <summary>
    /// Last published fingerprint per match. Only ever touched by the single polling loop below,
    /// which is why it is a plain dictionary.
    /// </summary>
    private readonly Dictionary<string, string> _published = new(StringComparer.OrdinalIgnoreCase);

    private bool _reserveWarningLogged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(options.Value.PollSeconds);
        var idle = TimeSpan.FromSeconds(options.Value.IdleSeconds);

        logger.LogInformation(
            "Live poller started; polling every {PollSeconds}s while clients are connected",
            options.Value.PollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!broadcaster.HasSubscribers)
            {
                if (!await DelayAsync(idle, stoppingToken))
                {
                    break;
                }

                continue;
            }

            if (budget.IsReserveOnly)
            {
                if (!_reserveWarningLogged)
                {
                    var (used, limit) = budget.Snapshot();
                    logger.LogWarning(
                        "Live poller paused: {Used} of {Limit} daily calls used, holding the rest back for page loads",
                        used,
                        limit);

                    _reserveWarningLogged = true;
                }
            }
            else
            {
                _reserveWarningLogged = false;
                await PollOnceAsync(stoppingToken);
            }

            if (!await DelayAsync(poll, stoppingToken))
            {
                break;
            }
        }

        logger.LogInformation("Live poller stopped");
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<MatchDetailsDto> window;

        // The provider is scoped, so the poller takes a scope per tick rather than capturing one
        // for the lifetime of the application.
        using var scope = scopes.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICricketDataProvider>();
        var enrichment = scope.ServiceProvider.GetRequiredService<IMatchEnrichmentProvider>();

        try
        {
            window = await provider.GetCurrentMatchesAsync(cancellationToken);
        }
        catch (CricketDataUnavailableException exception)
        {
            // Clients keep whatever they last received. Dropping their connection because one poll
            // failed would be worse than letting the score go briefly stale.
            logger.LogWarning(exception, "Live poll skipped: the cricket data provider is unavailable");
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var changed = new List<MatchDetailsDto>();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var bare in window)
        {
            present.Add(bare.Id);

            // Enrichment happens before the fingerprint, because the batters are part of what a
            // watcher notices changing. It costs nothing for the unmapped matches that make up
            // almost all of the window, and nothing at all while the second source is switched off.
            var match = bare.Status == MatchStatus.Live
                ? await Enrich.WithBattersAsync(bare, enrichment, cancellationToken)
                : bare;

            var signature = MatchSignature.For(match);

            if (_published.TryGetValue(match.Id, out var previous) && previous == signature)
            {
                continue;
            }

            _published[match.Id] = signature;
            changed.Add(match);
        }

        // Matches leave the provider's window when they age out. Without this the map would grow
        // for as long as the process lives.
        foreach (var gone in _published.Keys.Where(id => !present.Contains(id)).ToArray())
        {
            _published.Remove(gone);
        }

        if (changed.Count == 0)
        {
            return;
        }

        logger.LogInformation("Publishing {Count} changed match(es) to connected clients", changed.Count);
        broadcaster.Publish(changed);
    }

    private async Task<bool> DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, timeProvider, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
