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

    /// <remarks>
    /// <para>
    /// Two fallbacks to the archive, for two different situations that used to be one.
    /// </para>
    /// <para>
    /// <b>The provider answered and has never heard of this match</b> — its window has moved on. If
    /// we kept the match while it was still in the window, we can still answer, which is the whole
    /// point of having an archive.
    /// </para>
    /// <para>
    /// <b>The provider could not be reached at all.</b> This one was missing, and the gap was
    /// visible: during an outage, a match sitting in our archive answered 503 while the results
    /// list that linked to it answered 200 — so every result on the page was a dead link, and
    /// docs/api.md said in as many words that a 503 outside the live and upcoming lists is a bug.
    /// The exception path simply never reached the archive, because only a <see langword="null"/>
    /// did.
    /// </para>
    /// <para>
    /// A miss here rethrows rather than returning <see langword="null"/>. Not holding a match while
    /// the provider is unreachable means we cannot tell whether it exists, and a 404 would claim we
    /// can.
    /// </para>
    /// </remarks>
    public async Task<MatchDetailsDto?> GetMatchAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        MatchDetailsDto? match;

        try
        {
            match = await inner.GetMatchAsync(matchId, cancellationToken);
        }
        catch (CricketDataUnavailableException exception)
        {
            var held = await archive.GetAsync(matchId, cancellationToken);

            if (held is null)
            {
                throw;
            }

            logger.LogInformation(
                exception,
                "Serving match {MatchId} from the archive because the provider is unavailable",
                matchId);

            return held;
        }

        if (match is not null)
        {
            return await ArchiveThenReturnAsync(match, cancellationToken);
        }

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
