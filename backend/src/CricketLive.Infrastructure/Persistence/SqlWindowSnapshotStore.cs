using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// Stores the last good window in one row, and reads it back if it is recent enough to be worth
/// serving.
/// </summary>
internal sealed class SqlWindowSnapshotStore(
    CricketLiveDbContext database,
    IOptions<CricketData.CricketDataOptions> options,
    TimeProvider timeProvider,
    ILogger<SqlWindowSnapshotStore> logger) : IWindowSnapshotStore
{
    /// <summary>The same serialiser the archive uses, so a stored window reads back identically.</summary>
    private static readonly JsonSerializerOptions Format = JsonSerializerOptions.Web;

    public async Task SaveAsync(
        IReadOnlyList<MatchDetailsDto> matches,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken)
    {
        // An empty window is not worth keeping and is worth something worse than nothing: it would
        // overwrite a good copy with a blank one, and an outage would then recover no matches and
        // report them as a successfully recovered window.
        if (matches.Count == 0)
        {
            return;
        }

        try
        {
            var row = await database.WindowSnapshots
                .FirstOrDefaultAsync(entity => entity.Id == WindowSnapshotRow.SingletonId, cancellationToken);

            var payload = JsonSerializer.Serialize(matches, Format);

            if (row is null)
            {
                database.WindowSnapshots.Add(new WindowSnapshotRow
                {
                    Id = WindowSnapshotRow.SingletonId,
                    CapturedAtUtc = capturedAtUtc.UtcDateTime,
                    Payload = payload,
                });
            }
            else
            {
                row.CapturedAtUtc = capturedAtUtc.UtcDateTime;
                row.Payload = payload;
            }

            await database.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed write here must never fail the read that triggered it.
        catch (Exception exception)
        {
            // Same rule as the archive's writer: losing the fallback copy is a much smaller harm
            // than failing the page that was being served when we tried to keep it.
            logger.LogError(exception, "Could not store the window snapshot; serving the window regardless");
        }
#pragma warning restore CA1031
    }

    /// <remarks>
    /// <para>
    /// Refuses a copy older than <c>WindowSnapshotMaxAgeHours</c>, and that bound is the honest
    /// part of this class. A fixture list does not go stale — "3rd ODI, Friday 08:30" is as true an
    /// hour later — but a score in a match being played goes stale in minutes. Labelling covers the
    /// difference up to a point and then stops covering it, so past the bound this returns nothing
    /// and the endpoint reports the outage instead.
    /// </para>
    /// <para>
    /// Crests are rewritten on the way out for the same reason the archive does it: the stored
    /// payload is a faithful record of what was served, and what was served once pointed at the
    /// provider's own image host.
    /// </para>
    /// </remarks>
    public async Task<WindowSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        var row = await database.WindowSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == WindowSnapshotRow.SingletonId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        var age = timeProvider.GetUtcNow() - new DateTimeOffset(row.CapturedAtUtc, TimeSpan.Zero);

        if (age > TimeSpan.FromHours(options.Value.WindowSnapshotMaxAgeHours))
        {
            logger.LogWarning(
                "The stored window is {Hours:F1}h old, past the {Limit}h limit, so it will not be served",
                age.TotalHours,
                options.Value.WindowSnapshotMaxAgeHours);

            return null;
        }

        try
        {
            var matches = JsonSerializer.Deserialize<MatchDetailsDto[]>(row.Payload, Format);

            if (matches is null || matches.Length == 0)
            {
                return null;
            }

            return new WindowSnapshot
            {
                Matches = [.. matches.Select(WithOurOwnCrests)],
                CapturedAtUtc = new DateTimeOffset(row.CapturedAtUtc, TimeSpan.Zero),
            };
        }
        catch (JsonException exception)
        {
            // The DTO changed shape under a row written by an older build. One unreadable snapshot
            // must not turn a recoverable outage into a failure.
            logger.LogError(exception, "The stored window could not be read back and was ignored");
            return null;
        }
    }

    private static MatchDetailsDto WithOurOwnCrests(MatchDetailsDto match)
        => match with
        {
            Home = Rewritten(match.Home),
            Away = Rewritten(match.Away),
        };

    private static TeamInningsDto Rewritten(TeamInningsDto side)
        => side with { Team = side.Team with { LogoUrl = CrestUrl.ToProxyPath(side.Team.LogoUrl) } };
}
