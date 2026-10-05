namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// The single row holding the last window we read.
/// </summary>
/// <remarks>
/// One row, overwritten, with a fixed key — the same shape as <see cref="BackfillState"/> and for
/// the same reason: it can be addressed before it exists rather than having to be found. There is
/// no history here on purpose. The question this answers is "what did the provider last say", and
/// a second-oldest answer has no use.
/// </remarks>
internal sealed class WindowSnapshotRow
{
    public const int SingletonId = 1;

    public required int Id { get; set; }

    /// <summary>When the provider answered. The whole point of the row, alongside the payload.</summary>
    public required DateTime CapturedAtUtc { get; set; }

    /// <summary>The serialised <c>MatchDetailsDto</c> array, exactly as the window was served.</summary>
    public required string Payload { get; set; }
}
