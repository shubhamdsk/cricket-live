using System.ComponentModel.DataAnnotations;

namespace CricketLive.Infrastructure.Live;

public sealed class LiveOptions
{
    public const string SectionName = "Live";

    /// <summary>
    /// How often the poller asks for a fresh window while someone is watching. This is not the
    /// provider call rate: the provider is shielded by <c>CurrentMatchesCacheSeconds</c>, so
    /// ticking faster than that costs nothing and only shortens the gap between a cache refresh
    /// and the push that follows it.
    /// </summary>
    [Range(5, 600)]
    public int PollSeconds { get; init; } = 30;

    /// <summary>
    /// How often the poller re-checks whether anyone has started watching. Costs nothing — it
    /// never touches the provider, it only looks at the subscriber count.
    /// </summary>
    [Range(1, 300)]
    public int IdleSeconds { get; init; } = 5;

    /// <summary>
    /// Comment frames sent down an otherwise silent connection. Proxies commonly close an idle
    /// response somewhere around a minute, so this has to stay comfortably under that.
    /// </summary>
    [Range(5, 120)]
    public int HeartbeatSeconds { get; init; } = 20;
}
