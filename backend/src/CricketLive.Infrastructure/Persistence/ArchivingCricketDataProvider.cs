using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// Keeps a copy of every finished match that passes through, then behaves exactly like the provider
/// it wraps.
/// </summary>
/// <remarks>
/// <para>
/// Archiving sits here rather than inside the provider because fetching cricket and keeping cricket
/// are different jobs, and rather than in the poller because the poller only runs while somebody is
/// watching a live match — history would then depend on whether anyone happened to be watching.
/// Every window fetch flows through this type, so anyone opening the site contributes to the archive.
/// </para>
/// <para>
/// A failed write is never allowed to fail a read. Losing a match from history is a much smaller
/// harm than a home page that will not load.
/// </para>
/// </remarks>
internal sealed class ArchivingCricketDataProvider(
    ICricketDataProvider inner,
    IMatchArchive archive,
    ILogger<ArchivingCricketDataProvider> logger) : ICricketDataProvider
{
    public async Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(
        CancellationToken cancellationToken)
    {
        var window = await inner.GetCurrentMatchesAsync(cancellationToken);

        // No guard against repeat calls, because the archive's own "which of these do we already
        // hold" check is one indexed lookup over a handful of ids — cheaper than the state needed
        // to avoid it, and correct without having to stay in sync with the provider's cache.
        await TryArchiveAsync(window, cancellationToken);

        return window;
    }

    public async Task<MatchDetailsDto?> GetMatchAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        var match = await inner.GetMatchAsync(matchId, cancellationToken);

        if (match is not null)
        {
            return await ArchiveThenReturnAsync(match, cancellationToken);
        }

        // The provider's window has moved on. If we kept this match while it was still in the
        // window, we can still answer — which is the whole point of having an archive.
        return await archive.GetAsync(matchId, cancellationToken);
    }

    private async Task<MatchDetailsDto> ArchiveThenReturnAsync(
        MatchDetailsDto match,
        CancellationToken cancellationToken)
    {
        await TryArchiveAsync([match], cancellationToken);
        return match;
    }

    private async Task TryArchiveAsync(
        IReadOnlyList<MatchDetailsDto> matches,
        CancellationToken cancellationToken)
    {
        try
        {
            await archive.SaveFinishedAsync(matches, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not archive finished matches; serving the live window regardless");
        }
    }
}
