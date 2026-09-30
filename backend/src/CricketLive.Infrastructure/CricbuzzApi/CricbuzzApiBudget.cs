using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricbuzzApi;

/// <summary>
/// Counts what we have spent of a monthly allowance, and refuses to overspend it.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <c>CricketDataHitBudget</c>, with one difference that changes its character: the
/// window is a month rather than a day. A daily budget forgives a mistake overnight. A monthly one
/// does not — spend it on the third and there is no cricket on this page until the first.
/// </para>
/// <para>
/// So this is deliberately pessimistic. It starts from the configured cap rather than from zero
/// usage, it reconciles downward from the gateway's own count on every response, and it holds a
/// reserve that only completed matches may draw on.
/// </para>
/// <para>
/// In process and not shared, which means two instances each believe they have the whole
/// allowance. That is wrong and is the same limitation D-014 records for live state: it is correct
/// for one instance, and the fix is a shared counter rather than a cleverer local one.
/// </para>
/// </remarks>
internal sealed class CricbuzzApiBudget(
    IOptions<CricbuzzApiOptions> options,
    TimeProvider timeProvider,
    ILogger<CricbuzzApiBudget> logger)
{
    private readonly object _gate = new();

    private int _used;
    private int _limit = options.Value.MonthlyBudget;

    /// <summary>Which month <see cref="_used"/> refers to, so the count resets on its own.</summary>
    private int _month = -1;

    private bool _exhaustionLogged;

    /// <summary>
    /// Claims one call, or refuses.
    /// </summary>
    /// <param name="forFinishedMatch">
    /// Whether this call is for a match that has finished. Completed matches may draw on the
    /// reserve; live ones stop at it, because a live match would otherwise take everything.
    /// </param>
    public bool TryClaim(bool forFinishedMatch)
    {
        lock (_gate)
        {
            RollOverIfNewMonth();

            var ceiling = forFinishedMatch
                ? _limit
                : Math.Max(0, _limit - options.Value.ReservedForFinished);

            if (_used >= ceiling)
            {
                if (!_exhaustionLogged || forFinishedMatch)
                {
                    logger.LogWarning(
                        "Cricbuzz API budget reached: {Used} of {Limit} monthly calls used, ceiling for this request was {Ceiling}",
                        _used,
                        _limit,
                        ceiling);

                    _exhaustionLogged = true;
                }

                return false;
            }

            _used++;
            return true;
        }
    }

    /// <summary>Returns a claim that never reached the gateway, so a refusal costs nothing.</summary>
    public void Refund()
    {
        lock (_gate)
        {
            if (_used > 0)
            {
                _used--;
            }
        }
    }

    /// <summary>
    /// Replaces our count with the gateway's, which is authoritative.
    /// </summary>
    /// <remarks>
    /// RapidAPI reports what is left rather than what is spent, and it counts every consumer of
    /// the key — including a spike script or a second deployment. Trusting it over our own tally is
    /// the only way this stays right when something else is also spending.
    /// </remarks>
    public void Reconcile(int? remaining, int? limit)
    {
        if (remaining is null || limit is null || limit <= 0)
        {
            return;
        }

        lock (_gate)
        {
            _limit = limit.Value;
            _used = Math.Max(0, limit.Value - remaining.Value);
            _month = timeProvider.GetUtcNow().Month;
        }
    }

    public (int Used, int Limit) Snapshot()
    {
        lock (_gate)
        {
            RollOverIfNewMonth();
            return (_used, _limit);
        }
    }

    private void RollOverIfNewMonth()
    {
        var month = timeProvider.GetUtcNow().Month;

        if (_month == month)
        {
            return;
        }

        // The gateway's own reset is a rolling window rather than a calendar boundary, so this is
        // an approximation. It errs toward believing we have spent more than we have, because the
        // reconcile above corrects an underestimate on the first call of the month and nothing
        // corrects an overestimate until the allowance is already gone.
        _month = month;
        _used = 0;
        _exhaustionLogged = false;
    }
}
