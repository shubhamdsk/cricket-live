using System.ComponentModel.DataAnnotations;

namespace CricketLive.Infrastructure.CricketData;

public sealed class CricketDataOptions
{
    public const string SectionName = "CricketData";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.cricapi.com/v1/";

    /// <summary>
    /// Supplied through user secrets in development and the <c>CricketData__ApiKey</c> environment
    /// variable elsewhere. It is never committed and never leaves the server — see docs/security.md.
    /// </summary>
    [Required(ErrorMessage =
        "CricketData:ApiKey is not configured. Run: dotnet user-secrets set \"CricketData:ApiKey\" \"<key>\" --project src/CricketLive.Api. See README.md.")]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>The provider's free plan allows 100 calls per day, counted per key rather than per user.</summary>
    [Range(1, 1_000_000)]
    public int DailyHitBudget { get; set; } = 100;

    /// <summary>
    /// Calls held back from the polling loop so a match page opened late in the day can still be served.
    /// </summary>
    [Range(0, 1000)]
    public int ReservedHits { get; set; } = 10;

    /// <summary>
    /// How long a current-matches response is reused. At 100 calls a day this is the setting that
    /// decides whether we run out before the cricket does.
    /// </summary>
    [Range(15, 3600)]
    public int CurrentMatchesCacheSeconds { get; set; } = 300;

    /// <summary>A finished match cannot change, so its detail is worth caching until the process restarts.</summary>
    [Range(1, 168)]
    public int FinishedMatchCacheHours { get; set; } = 24;

    /// <summary>
    /// How many pages of the provider's series index to read, at twenty-five series a page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The index is around 1190 series deep, so reading all of it is 48 calls out of a hundred a
    /// day for one list. It is ordered start-date descending, so the early pages are the current
    /// season and this number is really "how far back to go". Four pages reached roughly a year
    /// back when it was measured.
    /// </para>
    /// <para>
    /// Multiply this by 24 divided by <see cref="SeriesIndexCacheHours"/> to get the daily cost:
    /// the defaults are sixteen calls, against a budget of a hundred that the poller currently
    /// leaves almost untouched.
    /// </para>
    /// </remarks>
    [Range(0, 48)]
    public int SeriesIndexPages { get; set; } = 4;

    /// <summary>
    /// How long the series index is reused. Series do not begin and end quickly, so this is hours
    /// rather than the minutes that the live window needs.
    /// </summary>
    [Range(1, 168)]
    public int SeriesIndexCacheHours { get; set; } = 6;

    /// <summary>
    /// How long one series' fixture list is reused, and whether it is read at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fixture list changes when a match finishes or is rescheduled, so this is shorter than the
    /// index. The cost is one call per series somebody opens, not one per series listed, which is
    /// what makes it affordable: the page that benefits is the page that pays.
    /// </para>
    /// <para>
    /// Zero switches the source off entirely and leaves held matches as the only source, which is
    /// where the series page started. See <c>AddSeriesFixtures</c>.
    /// </para>
    /// </remarks>
    [Range(0, 168)]
    public int SeriesFixturesCacheHours { get; set; } = 3;
}
