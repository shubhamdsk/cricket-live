using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// Reads the provider's window for a page that has something else to fall back on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by watching the deployed site refuse to serve a page it could have served.</b> With the
/// day's provider allowance spent and a freshly restarted container, every archive-backed endpoint
/// returned 503 — results, series, teams, search — while the archive sitting behind them held the
/// answers and needed no provider call to read. The whole reason the archive exists is that the
/// provider's window is narrow and temporary, and that reason was being defeated by letting one
/// failed window read fail the request.
/// </para>
/// <para>
/// So the window is optional for any page that merges it with something durable. It contributes
/// what is in play right now, and contributing nothing makes such a page slightly stale rather than
/// absent.
/// </para>
/// <para>
/// <b>Not used by the live and upcoming lists.</b> Those have no second source, so an empty list
/// there would be a claim that no cricket is on — which is a different statement from "we cannot
/// currently tell you", and only one of them is true during an outage. They still fail loudly.
/// </para>
/// </remarks>
internal static class ProviderWindow
{
    /// <summary>
    /// The current window, or an empty one if the provider could not be read.
    /// </summary>
    /// <remarks>
    /// Nothing is logged here on purpose. Every path that raises
    /// <see cref="CricketDataUnavailableException"/> has already logged the cause with the detail
    /// that matters — the status code, the refusal reason, or the exhausted allowance — and a second
    /// line saying a caller swallowed it would add no information and appear once per page view.
    /// </remarks>
    public static async Task<IReadOnlyList<MatchDetailsDto>> OrEmptyAsync(
        ICricketDataProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetCurrentMatchesAsync(cancellationToken);
        }
        catch (CricketDataUnavailableException)
        {
            return [];
        }
    }
}
