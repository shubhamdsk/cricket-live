using System.Threading.Channels;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Live;

/// <summary>
/// One connected client's view of one match. Disposing it detaches the client, which is what stops
/// the poller once the last person watching has gone.
/// </summary>
public sealed class MatchSubscription(
    ChannelReader<MatchDetailsDto> reader,
    Action onDispose) : IDisposable
{
    private int _disposed;

    public ChannelReader<MatchDetailsDto> Reader { get; } = reader;

    public void Dispose()
    {
        // A client that drops mid-write can reach here twice; detaching twice would under-count
        // subscribers and stop the poller while people are still watching.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            onDispose();
        }
    }
}
