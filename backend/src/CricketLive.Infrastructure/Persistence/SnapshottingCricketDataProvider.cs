using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// Keeps the last good window in the database, and serves it when the provider cannot be reached.
/// </summary>
/// <remarks>
/// <para>
/// <b>This replaces an in-memory last-known-good cache that failed on the day it was needed.</b>
/// The provider held its own fallback copy in <c>IMemoryCache</c>, which is exactly as durable as
/// the process — and the host rebuilds this container whenever it has been idle. So an allowance
/// exhausted overnight plus a deploy meant the home page showed two error cards for hours with no
/// fallback anywhere, while the results list below them answered normally from the archive.
/// </para>
/// <para>
/// <b>One fallback rather than two, deliberately.</b> Keeping the in-memory copy as well would
/// have been cheaper on a warm process, and it would also have made the capture time a lie:
/// a decorator cannot tell a freshly fetched window from an inner cache's stale one, so it would
/// have re-stamped recovered data as current and labelled nothing. A truthful timestamp is the
/// entire value here, so the cheap layer went.
/// </para>
/// <para>
/// Sits outside <see cref="ArchivingCricketDataProvider"/> so that what gets stored is what a
/// caller would have been given, and inside the scope filter so the stored copy is the provider's
/// window rather than our view of it — widening the coverage later should not be limited by what a
/// snapshot happened to keep.
/// </para>
/// </remarks>
internal sealed class SnapshottingCricketDataProvider(
    ICricketDataProvider inner,
    IWindowSnapshotStore snapshots,
    WindowFreshness freshness,
    TimeProvider timeProvider,
    ILogger<SnapshottingCricketDataProvider> logger) : ICricketDataProvider
{
    public async Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var window = await inner.GetCurrentMatchesAsync(cancellationToken);

            await snapshots.SaveAsync(window, timeProvider.GetUtcNow(), cancellationToken);

            return window;
        }
        catch (CricketDataUnavailableException exception)
        {
            var stored = await snapshots.LoadAsync(cancellationToken);

            if (stored is null)
            {
                // Nothing to fall back on, so the outage is the answer. This is the path that
                // produces the 503 on the live and upcoming lists, and it is correct: saying
                // nothing is on would be a claim we cannot support.
                throw;
            }

            // Read by the endpoints, so what they return is labelled rather than passed off as
            // current. Without this the recovery would be a quiet substitution.
            freshness.MarkRecovered(stored.CapturedAtUtc);

            logger.LogWarning(
                exception,
                "Serving {Count} match(es) from the window stored at {CapturedAt:u} because the provider is unavailable",
                stored.Matches.Count,
                stored.CapturedAtUtc);

            return stored.Matches;
        }
    }

    /// <remarks>
    /// Not snapshotted. A single match outside the window is already covered by the archive, which
    /// <see cref="ArchivingCricketDataProvider"/> falls back to on this same exception, and storing
    /// one here would be a second copy of a record we already keep properly.
    /// </remarks>
    public Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken)
        => inner.GetMatchAsync(matchId, cancellationToken);
}
