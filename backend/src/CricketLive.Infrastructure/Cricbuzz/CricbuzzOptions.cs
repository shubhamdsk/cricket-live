using System.ComponentModel.DataAnnotations;

namespace CricketLive.Infrastructure.Cricbuzz;

public sealed class CricbuzzOptions
{
    public const string SectionName = "Cricbuzz";

    /// <summary>
    /// Off unless switched on deliberately.
    /// </summary>
    /// <remarks>
    /// This reads a public website rather than an API offered to us, so whether to run it is a
    /// judgement about someone else's terms and not a technical default. Shipping it enabled would
    /// make that decision silently on behalf of whoever deploys this.
    /// </remarks>
    public bool Enabled { get; init; }

    [Required]
    public string BaseUrl { get; init; } = "https://www.cricbuzz.com";

    [Range(1, 30)]
    public int TimeoutSeconds { get; init; } = 8;

    /// <summary>
    /// How long one match's batters are reused.
    /// </summary>
    /// <remarks>
    /// Short, because fresh batters are the only reason this exists, but never zero: the cache is
    /// what stops a page of viewers becoming a page of requests to a site that never agreed to
    /// serve them.
    /// </remarks>
    [Range(5, 300)]
    public int CacheSeconds { get; init; } = 20;

    /// <summary>
    /// Identifies us honestly.
    /// </summary>
    /// <remarks>
    /// The project this was ported from sends a browser string with <c>Referer</c> and <c>Origin</c>
    /// set to cricbuzz.com, so its traffic reads as the site's own. That is evasion, and it is not
    /// reproduced here. If an honest agent gets blocked, that is a clear answer about whether this
    /// data is ours to take, and the right response is to stop rather than to disguise the caller.
    /// </remarks>
    [Required]
    public string UserAgent { get; init; } = "cricket-live/1.0 (+https://github.com/shubhamdsk/cricket-live)";

    /// <summary>
    /// Our match id to the Cricbuzz match id, written by hand.
    /// </summary>
    /// <remarks>
    /// Consulted before the listing is, so it doubles as an override for the cases where automatic
    /// resolution declines or gets it wrong. Normally empty.
    /// </remarks>
    public IReadOnlyDictionary<string, string> MatchIds { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether to pair matches automatically from Cricbuzz's own listing pages.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Enabled"/> because it is a separate risk. Resolution keys on the
    /// match title and series name, which the two providers happen to render identically, and it
    /// declines rather than guessing when the answer is not unique. Turning this off leaves
    /// <see cref="MatchIds"/> as the only way a match is ever enriched.
    /// </remarks>
    public bool AutoResolve { get; init; } = true;

    /// <summary>Listing pages to read match ids from, in order of preference.</summary>
    public IReadOnlyList<string> ListingPaths { get; init; } =
    [
        "cricket-match/live-scores",
        "cricket-schedule/upcoming-series/international",
    ];

    /// <summary>
    /// How long the listing is reused.
    /// </summary>
    /// <remarks>
    /// Long, deliberately. Which fixtures exist changes over hours, not seconds — it is the scores
    /// that move, and those are fetched separately. One listing read serves every match.
    /// </remarks>
    [Range(1, 720)]
    public int DirectoryCacheMinutes { get; init; } = 30;

    /// <summary>
    /// Whether to read series points tables, which no source available to us otherwise publishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate switch from <see cref="Enabled"/> because it carries a separate and larger
    /// obligation. Cricbuzz's <c>robots.txt</c> disallows every agent it has not named, and ours
    /// is not among them; reading these pages goes against that, knowingly, on the judgement that
    /// the file is a crawling convention rather than a licence. That judgement belongs to whoever
    /// deploys this, which is why it is off by default and why it is not folded into the switch
    /// that governs batters. Reasoning and evidence in docs/decisions.md, D-020.
    /// </para>
    /// <para>
    /// What this does <b>not</b> do, under any setting, is send a user agent the file permits.
    /// Declining a stated preference and impersonating someone who was granted an exception are
    /// different acts, and only the first is on the table.
    /// </para>
    /// </remarks>
    public bool StandingsEnabled { get; init; }

    /// <summary>
    /// How long one series' points table is reused.
    /// </summary>
    /// <remarks>
    /// Hours rather than seconds. A table changes when a match finishes, so reading it more often
    /// than that is pure cost to a site that did not ask for the traffic, and the long cache is
    /// most of what keeps this to a handful of requests a day.
    /// </remarks>
    [Range(5, 1440)]
    public int StandingsCacheMinutes { get; init; } = 180;

    /// <summary>Listing pages to discover series links from.</summary>
    public IReadOnlyList<string> SeriesListingPaths { get; init; } =
    [
        "cricket-schedule/upcoming-series/international",
        "cricket-match/live-scores",
    ];
}
