using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Keeps our daily call count inside the provider's free allowance.
/// </summary>
/// <remarks>
/// Every response carries the provider's own <c>hitsToday</c>, which is authoritative and survives
/// our restarts, so we correct to it whenever we see it and only estimate in between.
/// </remarks>
internal sealed class CricketDataHitBudget(
    IOptions<CricketDataOptions> options,
    TimeProvider timeProvider,
    ILogger<CricketDataHitBudget> logger)
{
    private readonly Lock _gate = new();
    private DateOnly _day;
    private int _used;
    private int _limit = options.Value.DailyHitBudget;

    /// <summary>
    /// Claims one call. Returns <see langword="false"/> when the allowance is spent, in which case the
    /// caller must serve what it already has rather than making the request.
    /// </summary>
    public bool TryClaim()
    {
        lock (_gate)
        {
            RollOverIfNewDay();

            if (_used >= _limit)
            {
                logger.LogWarning(
                    "Cricket data allowance exhausted: {Used} of {Limit} calls used today",
                    _used,
                    _limit);

                return false;
            }

            _used++;
            return true;
        }
    }

    /// <summary>
    /// Returns a claim that was never spent, because no request left the process. Without this an
    /// open circuit would eat the day's allowance while talking to nobody.
    /// </summary>
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

    /// <summary>Corrects our estimate to the provider's count, which is the one that matters.</summary>
    public void Reconcile(int hitsToday, int hitsLimit)
    {
        lock (_gate)
        {
            RollOverIfNewDay();

            if (hitsToday > 0)
            {
                _used = hitsToday;
            }

            if (hitsLimit > 0)
            {
                _limit = Math.Min(hitsLimit, options.Value.DailyHitBudget);
            }
        }
    }

    /// <summary>
    /// True once only the reserve is left. The background poller stops here so that a visitor
    /// opening a match page still gets a live answer.
    /// </summary>
    public bool IsReserveOnly
    {
        get
        {
            lock (_gate)
            {
                RollOverIfNewDay();
                return _used >= _limit - options.Value.ReservedHits;
            }
        }
    }

    public (int Used, int Limit) Snapshot()
    {
        lock (_gate)
        {
            RollOverIfNewDay();
            return (_used, _limit);
        }
    }

    private void RollOverIfNewDay()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (today != _day)
        {
            _day = today;
            _used = 0;
        }
    }
}
