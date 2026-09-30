using System.ComponentModel.DataAnnotations;

namespace CricketLive.Infrastructure.CricbuzzApi;

/// <summary>
/// Settings for the RapidAPI Cricbuzz listing, which is where scorecards come from.
/// </summary>
/// <remarks>
/// Read docs/decisions.md D-024 and D-026 before turning this on. The data is real and live —
/// measured advancing nine balls in seventy seconds — but the listing is an unlicensed scraper of
/// Cricbuzz rather than a Cricbuzz product, and the free tier's 200 calls a month is not enough to
/// follow a match. Both of those are judgements for whoever deploys this, which is why this ships
/// off and why the budget below is small by default.
/// </remarks>
public sealed class CricbuzzApiOptions
{
    public const string SectionName = "CricbuzzApi";

    /// <summary>Off unless switched on deliberately, for the reasons in the remarks above.</summary>
    public bool Enabled { get; init; }

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://cricbuzz-cricket.p.rapidapi.com";

    /// <summary>
    /// The RapidAPI host header, which the gateway requires alongside the key.
    /// </summary>
    /// <remarks>
    /// Configurable because the same key reaches every listing on the marketplace, and the host is
    /// the only thing that says which one you meant.
    /// </remarks>
    [Required]
    public string Host { get; init; } = "cricbuzz-cricket.p.rapidapi.com";

    /// <summary>
    /// Supplied through user secrets in development and <c>CricbuzzApi__ApiKey</c> elsewhere.
    /// </summary>
    /// <remarks>
    /// Deliberately has no placeholder in <c>appsettings.json</c>: an empty string committed to a
    /// public repository is a habit, and habits get filled in. Empty here simply means the feature
    /// cannot run, which <see cref="Enabled"/> already implies.
    /// </remarks>
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Calls allowed per calendar month.
    /// </summary>
    /// <remarks>
    /// The free plan's hard cap is 200. Reconciled against the gateway's own
    /// <c>x-ratelimit-requests-remaining</c> header on every response, so a stale or optimistic
    /// value here corrects itself after one call rather than overspending all month.
    /// </remarks>
    [Range(1, 10_000_000)]
    public int MonthlyBudget { get; init; } = 200;

    /// <summary>
    /// Calls held back from live matches so a finished one can still be read.
    /// </summary>
    /// <remarks>
    /// The same idea as <c>CricketDataOptions.ReservedHits</c> and a sharper version of the same
    /// problem. A live match will consume everything it is allowed, because it keeps changing; a
    /// finished match is worth one call ever and then caches for a day. Without a reserve the
    /// first live match of the month takes the whole budget and every completed match afterwards
    /// shows nothing.
    /// </remarks>
    [Range(0, 1_000_000)]
    public int ReservedForFinished { get; init; } = 60;

    /// <summary>
    /// How long a live match's scorecard is reused.
    /// </summary>
    /// <remarks>
    /// Long by the standards of live sport, and it has to be. At 200 calls a month, a thirty-second
    /// refresh would spend the lot in under two hours. Five minutes means a match page is honest
    /// about being a few overs behind rather than pretending to be live and going dark by lunchtime.
    /// </remarks>
    [Range(30, 3600)]
    public int LiveCacheSeconds { get; init; } = 300;

    /// <summary>
    /// How long a finished match's scorecard is reused.
    /// </summary>
    /// <remarks>
    /// A completed scorecard cannot change, so this is only bounded by how much memory we want to
    /// spend remembering it.
    /// </remarks>
    [Range(1, 168)]
    public int FinishedCacheHours { get; init; } = 24;
}
