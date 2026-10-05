using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// The last window we successfully read, and when we read it.
/// </summary>
public sealed record WindowSnapshot
{
    public required IReadOnlyList<MatchDetailsDto> Matches { get; init; }

    /// <summary>When the provider answered, not when this was read back.</summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }
}

/// <summary>
/// Keeps the last good window somewhere that outlives the process.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists because the in-memory version did not survive the thing it was for.</b> The
/// provider's window already had a last-known-good cache held in <c>IMemoryCache</c>, and it was
/// useless on the day it was needed: the host rebuilds this container whenever it has been idle,
/// so an outage that outlasts a restart finds an empty cache. The site spent hours showing two
/// error cards above a working results list, with a perfectly good window sitting in a cache that
/// no longer existed.
/// </para>
/// <para>
/// Deliberately not the match archive. That holds finished matches only, and the panels that break
/// during an outage are the live and upcoming ones — the two things the archive by definition
/// cannot answer.
/// </para>
/// </remarks>
public interface IWindowSnapshotStore
{
    /// <summary>Records a window we have just read. Never throws; a failed write is not a failed read.</summary>
    Task SaveAsync(
        IReadOnlyList<MatchDetailsDto> matches,
        DateTimeOffset capturedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>The last window we stored, or <see langword="null"/> if there is none worth serving.</summary>
    Task<WindowSnapshot?> LoadAsync(CancellationToken cancellationToken);
}
