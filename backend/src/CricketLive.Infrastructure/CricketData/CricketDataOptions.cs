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
}
