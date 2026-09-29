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
    /// Our match id to the Cricbuzz match id, maintained by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two providers share no key. The alternative to writing pairs down is guessing from team
    /// abbreviations and start times, and a wrong guess does not fail — it silently shows one
    /// match's batters on another match's page, which is worse than showing none.
    /// </para>
    /// <para>
    /// This doubles as the rate limiter. Only a match somebody listed here is ever fetched, so the
    /// load on a site that never agreed to serve us is bounded by an act of typing rather than by
    /// how popular the app becomes.
    /// </para>
    /// </remarks>
    public IReadOnlyDictionary<string, string> MatchIds { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
