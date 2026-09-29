using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Live;

/// <summary>
/// Fans one provider response out to every connected client. The whole point of the indirection is
/// that the number of people watching a match has no bearing on how often we call the provider.
/// </summary>
public interface IMatchBroadcaster
{
    /// <summary>
    /// False when nobody is watching anything, which is the poller's signal to stop spending calls.
    /// </summary>
    bool HasSubscribers { get; }

    /// <summary>Attach a client to one match. Dispose the result to detach.</summary>
    MatchSubscription Subscribe(string matchId);

    /// <summary>
    /// Hand a freshly polled window to whoever is watching. Matches nobody is watching are dropped
    /// here rather than filtered by the caller.
    /// </summary>
    void Publish(IReadOnlyList<MatchDetailsDto> matches);
}
