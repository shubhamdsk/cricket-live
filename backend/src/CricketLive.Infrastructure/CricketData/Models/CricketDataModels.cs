using System.Text.Json.Serialization;

namespace CricketLive.Infrastructure.CricketData.Models;

/// <summary>
/// Shapes returned by api.cricapi.com. These are the provider's vocabulary, not ours, and they are
/// deliberately <see langword="internal"/> so no other project can depend on them.
/// </summary>
internal sealed class CricketDataEnvelope<T>
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Populated only on failure, for example "Invalid API Key".</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("info")]
    public CricketDataInfo? Info { get; set; }

    public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
}

internal sealed class CricketDataInfo
{
    [JsonPropertyName("hitsToday")]
    public int HitsToday { get; set; }

    [JsonPropertyName("hitsLimit")]
    public int HitsLimit { get; set; }

    [JsonPropertyName("totalRows")]
    public int TotalRows { get; set; }
}

internal sealed class CricketDataMatch
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Composite line such as "India vs West Indies, 1st ODI, West Indies tour of India, 2026".</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>One of "test", "odi", "t20", or an unlisted value for other formats.</summary>
    [JsonPropertyName("matchType")]
    public string? MatchType { get; set; }

    /// <summary>Human sentence such as "India won by 8 wkts" or "Match drawn".</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("venue")]
    public string? Venue { get; set; }

    /// <summary>ISO timestamp with no offset; the provider means UTC.</summary>
    [JsonPropertyName("dateTimeGMT")]
    public string? DateTimeGmt { get; set; }

    [JsonPropertyName("teams")]
    public List<string>? Teams { get; set; }

    [JsonPropertyName("teamInfo")]
    public List<CricketDataTeamInfo>? TeamInfo { get; set; }

    [JsonPropertyName("score")]
    public List<CricketDataScore>? Score { get; set; }

    /// <summary>
    /// The provider's own identifier for the series this match belongs to.
    /// </summary>
    /// <remarks>
    /// Worth more than the series name, which reaches us only as the tail of the free-text
    /// <see cref="Name"/> and therefore carries every inconsistency in that string. Two matches in
    /// one series always agree on this value even when their names do not.
    /// </remarks>
    [JsonPropertyName("series_id")]
    public string? SeriesId { get; set; }

    [JsonPropertyName("bbbEnabled")]
    public bool BallByBallEnabled { get; set; }

    [JsonPropertyName("hasSquad")]
    public bool HasSquad { get; set; }

    [JsonPropertyName("matchStarted")]
    public bool MatchStarted { get; set; }

    [JsonPropertyName("matchEnded")]
    public bool MatchEnded { get; set; }
}

internal sealed class CricketDataTeamInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("shortname")]
    public string? ShortName { get; set; }

    [JsonPropertyName("img")]
    public string? Image { get; set; }
}

internal sealed class CricketDataScore
{
    [JsonPropertyName("r")]
    public int Runs { get; set; }

    [JsonPropertyName("w")]
    public int Wickets { get; set; }

    /// <summary>Over notation, where 145.3 means 145 overs and 3 balls.</summary>
    [JsonPropertyName("o")]
    public decimal Overs { get; set; }

    /// <summary>
    /// Free text naming the batting side and the innings number. The provider is inconsistent here:
    /// it emits both "durham Inning 1" and "Middlesex,Northamptonshire Inning 1" within one match.
    /// </summary>
    [JsonPropertyName("inning")]
    public string? Inning { get; set; }
}
