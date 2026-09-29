using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// The only route cricket data takes into the application. Implementations live in Infrastructure
/// and are the sole place a third-party model may appear.
/// </summary>
public interface ICricketDataProvider
{
    /// <summary>
    /// Every match in the provider's current window — live, imminent and recently finished — from a
    /// single upstream call. Callers partition the result rather than asking three separate questions,
    /// because our request budget is counted in calls, not in matches.
    /// </summary>
    /// <remarks>
    /// Returns the detailed shape because the provider's window already carries every field the
    /// per-match endpoint does. Callers that only need <see cref="MatchDto"/> can treat it as one.
    /// </remarks>
    /// <exception cref="CricketDataUnavailableException">The provider could not be reached or refused the request.</exception>
    Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(CancellationToken cancellationToken);

    /// <summary>Returns a single match, or <see langword="null"/> when the provider has no match with that id.</summary>
    /// <exception cref="CricketDataUnavailableException">The provider could not be reached or refused the request.</exception>
    Task<MatchDetailsDto?> GetMatchAsync(string matchId, CancellationToken cancellationToken);
}
