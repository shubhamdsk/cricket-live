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

    /// <summary>
    /// How long the match index is reused, and whether it is read at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately the same five minutes as <see cref="CurrentMatchesCacheSeconds"/>, because it
    /// answers the same question about the same moment and the two are read together. Diverging
    /// would mean the live and upcoming lists on one page disagreed about what time it was.
    /// </para>
    /// <para>
    /// Zero switches the source off, and the upcoming list falls back to whatever the main window
    /// holds — which today is nothing. See <c>AddMatchIndex</c>.
    /// </para>
    /// </remarks>
    [Range(0, 1440)]
    public int MatchIndexCacheMinutes { get; set; } = 5;

    /// <summary>
    /// How deep into the provider's match list the backfill walks before starting again, in pages
    /// of twenty-five. Zero switches it off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Forty, having previously been zero while the happy path was still unobserved. A run against
    /// the live provider read offset 0, got twenty-five rows and archived twenty-four of them, and
    /// the half-allowance guard then stopped the loop at sixty-eight calls of a hundred — so both
    /// the path that works and the guard that bounds it have now been seen rather than reasoned
    /// about. D-036 records what the wait was for.
    /// </para>
    /// <para>
    /// The list is 15,531 matches deep, which is 621 pages, and walking all of it at a polite rate
    /// would take months and spend most of that time in seasons nobody will open. Forty pages is a
    /// thousand of the most recent matches — against the handful the archive accumulated on its own
    /// — and once reached it starts again from the top, so recent history stays complete rather
    /// than the walk inching ever further into the past.
    /// </para>
    /// <para>
    /// The provider orders this list by series, newest series first, <b>not</b> by date, so "the
    /// first forty pages" means the thousand matches of the most recently active series rather
    /// than the thousand most recent matches. Close enough to the same thing to be useful, and not
    /// the same thing.
    /// </para>
    /// </remarks>
    [Range(0, 621)]
    public int MatchBackfillPages { get; set; } = 40;

    /// <summary>
    /// How many pages the backfill may read in one UTC day.
    /// </summary>
    /// <remarks>
    /// Six, so a full lap of the default depth takes about a week and the cost is six of a hundred
    /// calls — a sixth of what is left after the allowance's own reserve. Counted in the database
    /// rather than in memory because the host restarts this container several times a day and an
    /// in-memory counter would reset with it.
    /// </remarks>
    [Range(1, 100)]
    public int MatchBackfillPagesPerDay { get; set; } = 6;

    /// <summary>
    /// How long between attempts.
    /// </summary>
    /// <remarks>
    /// This is not the spend rate — the daily cap is. Ticking more often than the cap allows only
    /// means the pages are read earlier in the day and the loop then finds nothing to do, which is
    /// what should happen on a host that may be asleep for hours at a time.
    /// </remarks>
    [Range(1, 1440)]
    public int MatchBackfillIntervalMinutes { get; set; } = 20;
}
