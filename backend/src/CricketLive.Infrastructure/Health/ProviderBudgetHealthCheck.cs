using CricketLive.Infrastructure.CricketData;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CricketLive.Infrastructure.Health;

/// <summary>
/// How much of today's provider allowance is left.
/// </summary>
/// <remarks>
/// <para>
/// <b>This deliberately does not call the provider.</b> Task <c>8.11</c> asks for "provider
/// reachability", and the obvious reading of that is a request — but the allowance is a hundred
/// calls a day shared with the poller, so a readiness probe every thirty seconds would spend the
/// entire day's budget on asking whether we still had any. A check that consumes the resource it
/// is monitoring reports only on itself.
/// </para>
/// <para>
/// What is reported instead is the budget, which is the state that actually degrades. It comes
/// from the count the provider returns on every real response, so it is their number rather than
/// our guess, and reading it costs nothing.
/// </para>
/// <para>
/// Genuine unreachability surfaces the honest way: a provider call that fails becomes a 503 from
/// the endpoint that needed it, logged where it happened. That is a better signal than a synthetic
/// ping, because it reports a failure someone actually hit.
/// </para>
/// </remarks>
internal sealed class ProviderBudgetHealthCheck(CricketDataHitBudget budget) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var (used, limit) = budget.Snapshot();
        var remaining = Math.Max(0, limit - used);

        var data = new Dictionary<string, object>
        {
            ["used"] = used,
            ["limit"] = limit,
            ["remaining"] = remaining,
        };

        // Exhausted is degraded rather than unhealthy, and the distinction is not pedantic: every
        // list still answers from cache and the whole archive still reads, so the service works.
        // Reporting it unhealthy would take a working deployment out of a load balancer for the
        // rest of the day.
        if (remaining == 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Provider allowance spent for today. Cached and archived data still served.",
                data: data));
        }

        if (budget.IsReserveOnly)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Provider allowance down to its reserve. Background polling has stopped.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Provider allowance available.", data));
    }
}
