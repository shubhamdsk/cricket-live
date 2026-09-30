using System.Text.Json.Serialization;

namespace CricketLive.Infrastructure.CricbuzzApi.Models;

/// <summary>
/// The shape of <c>/mcenter/v1/{matchId}/hscard</c>, exactly as it arrives.
/// </summary>
/// <remarks>
/// <para>
/// Every property carries an explicit <see cref="JsonPropertyNameAttribute"/>, and that is not
/// belt-and-braces. This source names its fields in lowercase with no separators —
/// <c>strkrate</c>, <c>outdec</c>, <c>batteamsname</c>, <c>inningsid</c> — while the solution
/// serialises with <c>JsonSerializerOptions.Web</c>, which is camelCase. Without the attributes,
/// <c>StrkRate</c> would look for <c>strkRate</c>, find nothing, and bind <c>0</c>. A wrong
/// strike rate that looks plausible is worse than a crash, so the names are pinned rather than
/// derived.
/// </para>
/// <para>
/// Fields we do not use are omitted rather than modelled: the payload carries a dozen video and
/// premium-content properties per player that have nothing to do with cricket.
/// </para>
/// </remarks>
internal sealed class CricbuzzScorecardResponse
{
    [JsonPropertyName("scorecard")]
    public List<CricbuzzInnings>? Scorecard { get; set; }

    [JsonPropertyName("ismatchcomplete")]
    public bool IsMatchComplete { get; set; }

    /// <summary>The result or current state, as prose.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

internal sealed class CricbuzzInnings
{
    [JsonPropertyName("inningsid")]
    public int InningsId { get; set; }

    [JsonPropertyName("batsman")]
    public List<CricbuzzBatsman>? Batsman { get; set; }

    [JsonPropertyName("bowler")]
    public List<CricbuzzBowler>? Bowler { get; set; }

    /// <summary>Doubly wrapped upstream: <c>fow.fow[]</c>. Modelled as it is, not flattened here.</summary>
    [JsonPropertyName("fow")]
    public CricbuzzFowWrapper? Fow { get; set; }

    [JsonPropertyName("partnership")]
    public CricbuzzPartnershipWrapper? Partnership { get; set; }

    [JsonPropertyName("extras")]
    public CricbuzzExtras? Extras { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("wickets")]
    public int Wickets { get; set; }

    [JsonPropertyName("overs")]
    public double Overs { get; set; }

    [JsonPropertyName("runrate")]
    public double RunRate { get; set; }

    [JsonPropertyName("batteamname")]
    public string? BatTeamName { get; set; }

    [JsonPropertyName("batteamsname")]
    public string? BatTeamShortName { get; set; }

    [JsonPropertyName("isdeclared")]
    public bool IsDeclared { get; set; }
}

internal sealed class CricbuzzBatsman
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("runs")]
    public int Runs { get; set; }

    [JsonPropertyName("balls")]
    public int Balls { get; set; }

    [JsonPropertyName("fours")]
    public int Fours { get; set; }

    [JsonPropertyName("sixes")]
    public int Sixes { get; set; }

    [JsonPropertyName("strkrate")]
    public string? StrikeRate { get; set; }

    /// <summary>How they got out, in the source's own words, or <c>"not out"</c>, or empty.</summary>
    [JsonPropertyName("outdec")]
    public string? OutDescription { get; set; }

    [JsonPropertyName("iscaptain")]
    public bool IsCaptain { get; set; }

    [JsonPropertyName("iskeeper")]
    public bool IsKeeper { get; set; }
}

internal sealed class CricbuzzBowler
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>A string upstream, and left one: <c>"3.5"</c> is five balls, not half an over.</summary>
    [JsonPropertyName("overs")]
    public string? Overs { get; set; }

    [JsonPropertyName("maidens")]
    public int Maidens { get; set; }

    [JsonPropertyName("runs")]
    public int Runs { get; set; }

    [JsonPropertyName("wickets")]
    public int Wickets { get; set; }

    [JsonPropertyName("economy")]
    public string? Economy { get; set; }
}

internal sealed class CricbuzzFowWrapper
{
    [JsonPropertyName("fow")]
    public List<CricbuzzFow>? Fow { get; set; }
}

internal sealed class CricbuzzFow
{
    [JsonPropertyName("batsmanname")]
    public string? BatsmanName { get; set; }

    /// <summary>The team's score when this wicket fell.</summary>
    [JsonPropertyName("runs")]
    public int Runs { get; set; }

    [JsonPropertyName("overnbr")]
    public double OverNumber { get; set; }
}

internal sealed class CricbuzzPartnershipWrapper
{
    [JsonPropertyName("partnership")]
    public List<CricbuzzPartnership>? Partnership { get; set; }
}

internal sealed class CricbuzzPartnership
{
    [JsonPropertyName("bat1name")]
    public string? Bat1Name { get; set; }

    [JsonPropertyName("bat1runs")]
    public int Bat1Runs { get; set; }

    [JsonPropertyName("bat2name")]
    public string? Bat2Name { get; set; }

    [JsonPropertyName("bat2runs")]
    public int Bat2Runs { get; set; }

    [JsonPropertyName("totalruns")]
    public int TotalRuns { get; set; }

    [JsonPropertyName("totalballs")]
    public int TotalBalls { get; set; }
}

internal sealed class CricbuzzExtras
{
    [JsonPropertyName("byes")]
    public int Byes { get; set; }

    [JsonPropertyName("legbyes")]
    public int LegByes { get; set; }

    [JsonPropertyName("wides")]
    public int Wides { get; set; }

    [JsonPropertyName("noballs")]
    public int NoBalls { get; set; }

    [JsonPropertyName("penalty")]
    public int Penalty { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
