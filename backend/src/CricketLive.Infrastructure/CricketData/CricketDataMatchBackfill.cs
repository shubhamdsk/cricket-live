using System.Globalization;
using CricketLive.Application.Matches;
using CricketLive.Infrastructure.CricketData.Models;
using CricketLive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Walks the provider's <c>matches</c> list a page at a time and puts what it finds in the archive.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place in the project that fetches history rather than keeping what it was given.
/// It exists because the archive could only accumulate forward: it began the day this site did, so
/// the results page held two matches and the teams page two sides, and no amount of waiting fixes
/// a past that was never recorded.
/// </para>
/// <para>
/// <b>Why ingest rather than read through.</b> The results page filters, counts and pages in SQL
/// over the archive. Mixing provider rows into that at read time would mean two sources disagreeing
/// about what page three is and what the total is — a page of twenty that returns eleven, and a
/// count that does not match it. Writing the provider's rows into the archive instead leaves every
/// query exactly as it was, and widens the results page, the series pages, the teams page and
/// search in one go, because all of them already read the archive.
/// </para>
/// <para>
/// <b>Three guards, because the allowance is a hundred calls a day and this could eat all of it.</b>
/// A page count per day held in the database; a refusal to run once half the day's allowance is
/// already spent; and a bounded depth, so it laps over recent history instead of crawling towards
/// 2015. The site must stay more important than its history.
/// </para>
/// </remarks>
internal sealed class CricketDataMatchBackfill(
    IServiceScopeFactory scopes,
    CricketDataHitBudget budget,
    IOptions<CricketDataOptions> options,
    TimeProvider timeProvider,
    ILogger<CricketDataMatchBackfill> logger) : BackgroundService
{
    private const int PageSize = 25;

    /// <summary>
    /// Waited out before the first page, so a cold start serves requests before spending calls.
    /// </summary>
    /// <remarks>
    /// The host rebuilds this container whenever it has been idle, which means a start is nearly
    /// always somebody waiting for a page. History can wait two minutes; they cannot.
    /// </remarks>
    private static readonly TimeSpan Settle = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var depth = options.Value.MatchBackfillPages;

        if (depth <= 0)
        {
            logger.LogInformation("Match backfill is switched off");
            return;
        }

        logger.LogInformation(
            "Match backfill started; up to {PagesPerDay} page(s) a day over the first {Depth} page(s)",
            options.Value.MatchBackfillPagesPerDay,
            depth);

        if (!await DelayAsync(Settle, stoppingToken))
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(options.Value.MatchBackfillIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031 // Any failure here must not take the host down with it.
            catch (Exception exception)
            {
                // A background loop that throws stops forever, taking the archive's only source of
                // history with it, and nothing would report that it had happened.
                logger.LogError(exception, "Match backfill tick failed; it will try again later");
            }
#pragma warning restore CA1031

            if (!await DelayAsync(interval, stoppingToken))
            {
                break;
            }
        }

        logger.LogInformation("Match backfill stopped");
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        // Half the allowance, so the backfill can never be the reason a visitor gets a stale score.
        // This is a stricter line than the poller's reserve on purpose: the poller is serving
        // somebody who is watching, and this is serving nobody yet.
        var (used, limit) = budget.Snapshot();

        if (used >= limit / 2)
        {
            logger.LogDebug(
                "Match backfill waiting: {Used} of {Limit} daily calls already spent",
                used,
                limit);

            return;
        }

        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CricketLiveDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<CricketDataClient>();
        var mapper = scope.ServiceProvider.GetRequiredService<CricketDataMatchMapper>();
        var archive = scope.ServiceProvider.GetRequiredService<IMatchArchive>();

        var state = await LoadAsync(database, cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (state.PagesReadOn != today)
        {
            state.PagesReadOn = today;
            state.PagesReadToday = 0;
        }

        if (state.PagesReadToday >= options.Value.MatchBackfillPagesPerDay)
        {
            return;
        }

        var offset = state.NextOffset;

        List<CricketDataMatch>? rows;

        try
        {
            rows = await client.GetAsync<List<CricketDataMatch>>(
                "matches",
                new Dictionary<string, string>
                {
                    ["offset"] = offset.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken);
        }
        catch (CricketDataUnavailableException exception)
        {
            // The offset is deliberately not advanced, so the page is retried rather than skipped.
            // A page that fails every time would stall the lap, which is what LapsCompleted in the
            // log is there to make visible.
            logger.LogWarning(exception, "Match backfill could not read offset {Offset}", offset);
            return;
        }
        catch (CricketDataRejectedException exception)
        {
            // Observed in a real run: the first tick after a restart spends one doomed call, because
            // the budget is held in memory and a fresh process starts believing nothing has been
            // spent. The response corrects that belief, but on a host that rebuilds its container
            // several times a day the same doomed call is made several times.
            //
            // So an account refusal is written down as "no more pages today", which survives the
            // restart the in-memory count does not. A refusal about the request itself is only
            // logged, because that is a different problem and silencing the day would hide it.
            if (exception.IsAccountProblem)
            {
                state.PagesReadToday = options.Value.MatchBackfillPagesPerDay;
                state.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                await database.SaveChangesAsync(cancellationToken);
            }

            logger.LogWarning(exception, "The provider refused match offset {Offset}", offset);
            return;
        }

        // Counted even when the page turns out to be empty, because the call was still spent.
        state.PagesReadToday++;
        state.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        var matches = (rows ?? [])
            .Select(mapper.ToMatchDetails)
            .OfType<Application.Matches.Dtos.MatchDetailsDto>()
            .ToArray();

        // SaveFinishedAsync keeps only completed matches and ignores any it already holds, so a
        // page of fixtures or a page read twice is written as nothing at all.
        await archive.SaveFinishedAsync(matches, cancellationToken);

        var end = offset + PageSize;
        var exhausted = (rows ?? []).Count < PageSize;

        if (exhausted || end >= options.Value.MatchBackfillPages * PageSize)
        {
            state.NextOffset = 0;
            state.LapsCompleted++;

            logger.LogInformation(
                "Match backfill finished lap {Lap} at offset {Offset}; starting again from the top",
                state.LapsCompleted,
                offset);
        }
        else
        {
            state.NextOffset = end;
        }

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Match backfill read offset {Offset}: {Rows} row(s), {Finished} finished; {Today} page(s) today",
            offset,
            (rows ?? []).Count,
            matches.Length,
            state.PagesReadToday);
    }

    /// <summary>
    /// The single state row, created on first use.
    /// </summary>
    /// <remarks>
    /// Tracked rather than detached, so the caller's edits are saved by the same
    /// <c>SaveChangesAsync</c> that writes the page it just read. A single-instance deployment with
    /// one loop writing one row needs no more concurrency control than that, and if this ever runs
    /// twice the worst outcome is a page read twice, which the archive discards.
    /// </remarks>
    private static async Task<BackfillState> LoadAsync(
        CricketLiveDbContext database,
        CancellationToken cancellationToken)
    {
        var state = await database.BackfillState
            .FirstOrDefaultAsync(row => row.Id == BackfillState.SingletonId, cancellationToken);

        if (state is not null)
        {
            return state;
        }

        state = new BackfillState { Id = BackfillState.SingletonId };
        database.BackfillState.Add(state);

        return state;
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
