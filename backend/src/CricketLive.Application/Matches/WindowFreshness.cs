namespace CricketLive.Application.Matches;

/// <summary>
/// Whether the window behind this request was read just now or recovered from a stored copy.
/// </summary>
/// <remarks>
/// <para>
/// <b>A scoped note passed sideways, rather than a change to every signature between here and the
/// controller.</b> The alternative was wrapping the return type of
/// <see cref="ICricketDataProvider.GetCurrentMatchesAsync"/>, which five services and a background
/// poller consume — all of them to carry one nullable timestamp that only two endpoints read.
/// </para>
/// <para>
/// Set once per request at most, by the provider decorator that recovers a stored window, and read
/// by the live and upcoming endpoints to label what they return. Everything in between is
/// untouched and does not know this exists.
/// </para>
/// <para>
/// <b>Labelling is what makes serving stale data honest rather than merely convenient.</b> An
/// unlabelled old score is a lie told to someone watching a match; the same score under "as of
/// 05:12" is a fact about our records. The two are the same bytes and they are not the same thing.
/// </para>
/// </remarks>
public sealed class WindowFreshness
{
    /// <summary>
    /// When the data was actually read from the provider, or <see langword="null"/> if it is current.
    /// </summary>
    public DateTimeOffset? CapturedAtUtc { get; private set; }

    public bool IsStale => CapturedAtUtc is not null;

    public void MarkRecovered(DateTimeOffset capturedAtUtc) => CapturedAtUtc = capturedAtUtc;
}
