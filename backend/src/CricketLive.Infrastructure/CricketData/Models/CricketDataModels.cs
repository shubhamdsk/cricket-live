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

/// <summary>
/// One row of the provider's <c>cricScore</c> endpoint.
/// </summary>
/// <remarks>
/// <para>
/// A scoreboard strip rather than a match: it exists to be read at a glance, so it carries a score
/// and a state and almost nothing else. No series id, no venue, no match description, and a team
/// written as <c>"India [IND]"</c> with the name and the abbreviation run together in one string.
/// </para>
/// <para>
/// Only three fields are mapped, because only three are worth anything here. The <c>id</c> is the
/// same id the provider's other endpoints use, which is what makes this row joinable to a real
/// match; <c>series</c> names the series; <c>ms</c> says whether it is over. The rest —
/// <c>t1</c>, <c>t2</c>, <c>t1s</c>, <c>t2s</c>, <c>t1img</c>, <c>t2img</c>, <c>matchType</c>,
/// <c>status</c>, <c>dateTimeGMT</c> — are all available in better form from <c>series_info</c>,
/// and the two image fields are on the provider's own host, which <c>D-031</c> forbids us carrying
/// into a DTO at all.
/// </para>
/// </remarks>
internal sealed class CricketDataScoreboardRow
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Such as "West Indies tour of India, 2026". A name; there is no id to be had.</summary>
    [JsonPropertyName("series")]
    public string? Series { get; set; }

    /// <summary>
    /// Match state. Measured values: <c>"fixture"</c> for a match not yet played and
    /// <c>"result"</c> for one that is over.
    /// </summary>
    /// <remarks>
    /// Documented nowhere, so the set is not known to be closed and a live match was never
    /// observed — nothing was in play on any day this was probed. Read by asking whether it says
    /// the match is <i>finished</i>, so an unseen value such as a live or abandoned state reads as
    /// pending rather than being silently discarded.
    /// </remarks>
    [JsonPropertyName("ms")]
    public string? MatchState { get; set; }
}

/// <summary>
/// One row of the provider's <c>series</c> index.
/// </summary>
/// <remarks>
/// <para>
/// An index rather than a series: there are no standings here, no squads, and no teams. It is
/// read for one reason — to learn that a series exists at all, which matches alone cannot tell us
/// about a series none of whose matches are in the window or our archive.
/// </para>
/// <para>
/// <b><c>endDate</c> is deliberately not mapped.</b> The provider sends it as <c>"Apr 11"</c>,
/// with no year, for every row sampled — including rows whose <c>startDate</c> is a full ISO date.
/// Pairing a year to it would mean guessing, and a series that runs across New Year makes the
/// guess wrong in a way nobody would notice. Absent beats invented.
/// </para>
/// <para>
/// <b><c>startDate</c> arrives in two different formats</b> and which one you get is not random:
/// across fifty rows sampled, every row with <c>matches</c> above zero carried a full ISO date and
/// every row with <c>matches</c> of zero carried the year-less form. That correlation is what
/// <see cref="CricketDataSeriesIndex"/> filters on, and it is why the filter costs us nothing.
/// </para>
/// </remarks>
internal sealed class CricketDataSeries
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Such as "West Indies tour of India, 2026". Already the name our matches carry.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>A full ISO date, or <c>"Oct 18"</c>. See the remarks on this type.</summary>
    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    /// <summary>How many matches the provider holds for this series, which is not how many we hold.</summary>
    [JsonPropertyName("matches")]
    public int Matches { get; set; }
}

/// <summary>
/// The <c>series_info</c> response: a header, and every match of the series.
/// </summary>
/// <remarks>
/// <para>
/// This is the endpoint the <c>series</c> index is not. The rows in <see cref="MatchList"/> arrive
/// in the same shape as <see cref="CricketDataMatch"/> — a composite name carrying the match title,
/// a venue, teams and <c>teamInfo</c> — so the existing mapper reads them without changes and a
/// series page can show fixtures that have not been played yet.
/// </para>
/// <para>
/// <b>The rows carry no <c>series_id</c>.</b> Measured: zero of eight. That is not a problem so
/// long as the caller remembers it already knows the id — it had to supply one to ask the question
/// — and fills it in rather than letting the mapper record an empty series for every match.
/// </para>
/// </remarks>
internal sealed class CricketDataSeriesInfo
{
    [JsonPropertyName("info")]
    public CricketDataSeriesHeader? Info { get; set; }

    [JsonPropertyName("matchList")]
    public List<CricketDataMatch>? MatchList { get; set; }
}

/// <summary>
/// The header of a <c>series_info</c> response.
/// </summary>
/// <remarks>
/// <c>enddate</c> is as unusable here as it is in the index — measured as <c>"Oct 17"</c> while
/// <c>startdate</c> on the same object was <c>"2026-09-27"</c> — so it is not mapped. The per-format
/// counts are, because a series page can say "3 ODIs and 5 T20Is" from them without arithmetic.
/// </remarks>
internal sealed class CricketDataSeriesHeader
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("matches")]
    public int Matches { get; set; }

    [JsonPropertyName("odi")]
    public int Odi { get; set; }

    [JsonPropertyName("t20")]
    public int T20 { get; set; }

    [JsonPropertyName("test")]
    public int Test { get; set; }
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
