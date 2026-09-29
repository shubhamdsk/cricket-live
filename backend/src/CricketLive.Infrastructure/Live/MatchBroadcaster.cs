using System.Collections.Concurrent;
using System.Threading.Channels;
using CricketLive.Application.Live;
using CricketLive.Application.Matches.Dtos;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.Live;

/// <summary>
/// Holds every connected client in memory and hands each one the latest state of the match it
/// asked for. Deliberately not Redis: with a single API instance there is nothing to fan out
/// across, and a network hop would buy nothing. See D-014 for when that stops being true.
/// </summary>
internal sealed class MatchBroadcaster(ILogger<MatchBroadcaster> logger) : IMatchBroadcaster
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, ChannelWriter<MatchDetailsDto>>> _byMatch =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _gate = new();

    private int _subscriberCount;

    public bool HasSubscribers => Volatile.Read(ref _subscriberCount) > 0;

    public MatchSubscription Subscribe(string matchId)
    {
        // Capacity one, dropping the oldest: a client that has fallen behind wants the current
        // score, not a queue of the scores it missed. This is also what stops a stalled connection
        // from growing without bound.
        var channel = Channel.CreateBounded<MatchDetailsDto>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var id = Guid.NewGuid();

        lock (_gate)
        {
            var subscribers = _byMatch.GetOrAdd(matchId, _ => new ConcurrentDictionary<Guid, ChannelWriter<MatchDetailsDto>>());
            subscribers[id] = channel.Writer;
        }

        var total = Interlocked.Increment(ref _subscriberCount);
        logger.LogInformation("Client attached to match {MatchId}; {Total} now watching", matchId, total);

        return new MatchSubscription(channel.Reader, () => Unsubscribe(matchId, id, channel.Writer));
    }

    public void Publish(IReadOnlyList<MatchDetailsDto> matches)
    {
        foreach (var match in matches)
        {
            if (!_byMatch.TryGetValue(match.Id, out var subscribers))
            {
                continue;
            }

            foreach (var writer in subscribers.Values)
            {
                // Never awaited: a bounded drop-oldest channel always accepts, and a publisher must
                // not be slowed by the slowest reader on the list.
                writer.TryWrite(match);
            }
        }
    }

    private void Unsubscribe(string matchId, Guid id, ChannelWriter<MatchDetailsDto> writer)
    {
        writer.TryComplete();

        lock (_gate)
        {
            if (_byMatch.TryGetValue(matchId, out var subscribers))
            {
                subscribers.TryRemove(id, out _);

                // Under the same lock as Subscribe, so a match cannot be removed between another
                // client being added to it and that client's first update.
                if (subscribers.IsEmpty)
                {
                    _byMatch.TryRemove(matchId, out _);
                }
            }
        }

        var total = Interlocked.Decrement(ref _subscriberCount);
        logger.LogInformation("Client detached from match {MatchId}; {Total} still watching", matchId, total);
    }
}
