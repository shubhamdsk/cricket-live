using CricketLive.Application.Enrichment;
using CricketLive.Application.Scorecards;
using CricketLive.Application.Scorecards.Dtos;
using CricketLive.Infrastructure.Cricbuzz;
using CricketLive.Infrastructure.CricbuzzApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricbuzzApi;

/// <summary>
/// Reads scorecards from the RapidAPI Cricbuzz listing.
/// </summary>
/// <remarks>
/// <para>
/// Every decision here is shaped by 200 calls a month. It never polls; a scorecard is fetched only
/// because somebody asked for one. A finished match is fetched once and cached for a day, since it
/// cannot change. A live match is cached for five minutes, which is dishonest about being live but
/// honest about the budget — the alternative is a page that is genuinely live until the allowance
/// runs out on the third of the month and blank afterwards.
/// </para>
/// <para>
/// Match identity is borrowed rather than solved. Our ids are CricketData GUIDs and this source
/// uses Cricbuzz's integers, so the pairing comes from
/// <see cref="CricbuzzMatchDirectory"/> — already built, already cached for half an hour, and free,
/// because it reads a listing page rather than spending a gateway call. The consequence is worth
/// stating: turning this on also turns on reading Cricbuzz's listing pages, which is the subject of
/// D-020. There is no way to use this source without identifying matches in its terms.
/// </para>
/// </remarks>
internal sealed class CricbuzzApiScorecardProvider(
    CricbuzzApiClient client,
    CricbuzzApiBudget budget,
    CricbuzzMatchDirectory directory,
    IMemoryCache cache,
    IOptions<CricbuzzApiOptions> options,
    IOptions<CricbuzzOptions> cricbuzz,
    ILogger<CricbuzzApiScorecardProvider> logger) : IMatchScorecardProvider
{
    public async Task<ScorecardDto?> GetScorecardAsync(
        MatchIdentity match,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || options.Value.ApiKey.Length == 0)
        {
            return null;
        }

        var sourceMatchId = await ResolveAsync(match, cancellationToken);

        // Not knowing which Cricbuzz match this is remains the normal case, not a failure: the two
        // providers cover overlapping but different sets of cricket.
        if (sourceMatchId is null)
        {
            return null;
        }

        if (!IsWellFormed(sourceMatchId))
        {
            // Interpolated into a URL, so this is the boundary that stops a typo in configuration
            // steering the request somewhere other than a scorecard.
            logger.LogWarning(
                "Cricbuzz match id for {MatchId} is not a plain number; ignoring it",
                match.MatchId);

            return null;
        }

        var cacheKey = $"cricbuzz-api:scorecard:{sourceMatchId}";
        if (cache.TryGetValue(cacheKey, out ScorecardDto? cached))
        {
            return cached;
        }

        // Which cache lifetime and which budget ceiling both depend on whether the match has
        // finished, and we only learn that from the response. So the request is made on the
        // assumption it is live — the stricter ceiling — and the answer decides the rest.
        var response = await client.GetAsync<CricbuzzScorecardResponse>(
            $"/mcenter/v1/{sourceMatchId}/hscard",
            forFinishedMatch: false,
            cancellationToken);

        if (response?.Scorecard is null || response.Scorecard.Count == 0)
        {
            // Cached as a miss, and this is the important half of the budget story. Without it,
            // every viewer of a match this source does not carry costs another call, and a handful
            // of curious readers would empty the month.
            cache.Set(cacheKey, (ScorecardDto?)null, TimeSpan.FromHours(1));
            return null;
        }

        var scorecard = Map(match.MatchId, response);

        cache.Set(
            cacheKey,
            scorecard,
            scorecard.IsComplete
                ? TimeSpan.FromHours(options.Value.FinishedCacheHours)
                : TimeSpan.FromSeconds(options.Value.LiveCacheSeconds));

        var (used, limit) = budget.Snapshot();
        logger.LogInformation(
            "Read a {State} scorecard for match {MatchId}; {Used} of {Limit} monthly calls used",
            scorecard.IsComplete ? "completed" : "live",
            match.MatchId,
            used,
            limit);

        return scorecard;
    }

    /// <summary>
    /// The hand-written pair if there is one, otherwise whatever the listing can prove.
    /// </summary>
    /// <remarks>
    /// Shares <c>Cricbuzz:MatchIds</c> with the enrichment provider rather than introducing a
    /// second map, because both resolve the same question — which Cricbuzz match this is — and two
    /// answers to it would be one too many.
    /// </remarks>
    private async Task<string?> ResolveAsync(MatchIdentity match, CancellationToken cancellationToken)
    {
        if (cricbuzz.Value.MatchIds.TryGetValue(match.MatchId, out var configured))
        {
            return configured;
        }

        return cricbuzz.Value.AutoResolve
            ? await directory.ResolveAsync(match, cancellationToken)
            : null;
    }

    /// <summary>Digits only, because the id goes into a URL path.</summary>
    private static bool IsWellFormed(string sourceMatchId)
        => sourceMatchId.Length is > 0 and <= 12 && sourceMatchId.All(char.IsAsciiDigit);

    private static ScorecardDto Map(string matchId, CricbuzzScorecardResponse response) => new()
    {
        MatchId = matchId,
        Status = response.Status ?? string.Empty,
        IsComplete = response.IsMatchComplete,
        Innings = [.. (response.Scorecard ?? []).Select(MapInnings)],
    };

    private static InningsCardDto MapInnings(CricbuzzInnings innings) => new()
    {
        InningsNumber = innings.InningsId,
        BattingTeamName = innings.BatTeamName ?? string.Empty,
        BattingTeamShortName = innings.BatTeamShortName ?? string.Empty,
        Runs = innings.Score,
        Wickets = innings.Wickets,
        Overs = innings.Overs,
        RunRate = innings.RunRate,
        IsDeclared = innings.IsDeclared,

        // Anyone with a name. A card carries eleven entries whether or not they batted, and one
        // with no name is a placeholder rather than a player.
        Batting =
        [
            .. (innings.Batsman ?? [])
                .Where(batsman => !string.IsNullOrWhiteSpace(batsman.Name))
                .Select(MapBatting)
        ],

        Bowling =
        [
            .. (innings.Bowler ?? [])
                .Where(bowler => !string.IsNullOrWhiteSpace(bowler.Name))
                .Select(MapBowling)
        ],

        Extras = MapExtras(innings.Extras),

        // `WicketNumber` is the position in the list rather than a field: they arrive in the order
        // they fell, and the source does not number them.
        FallOfWickets =
        [
            .. (innings.Fow?.Fow ?? [])
                .Select((wicket, index) => new WicketDto
                {
                    BatterName = wicket.BatsmanName ?? string.Empty,
                    Runs = wicket.Runs,
                    WicketNumber = index + 1,
                    Over = wicket.OverNumber,
                })
        ],

        Partnerships =
        [
            .. (innings.Partnership?.Partnership ?? [])
                .Select(stand => new PartnershipDto
                {
                    FirstBatterName = stand.Bat1Name ?? string.Empty,
                    FirstBatterRuns = stand.Bat1Runs,
                    SecondBatterName = stand.Bat2Name ?? string.Empty,
                    SecondBatterRuns = stand.Bat2Runs,
                    Runs = stand.TotalRuns,
                    Balls = stand.TotalBalls,
                })
        ],
    };

    private static BattingLineDto MapBatting(CricbuzzBatsman batsman) => new()
    {
        Name = batsman.Name ?? string.Empty,
        Runs = batsman.Runs,
        Balls = batsman.Balls,
        Fours = batsman.Fours,
        Sixes = batsman.Sixes,

        // Passed through as text. Recomputing it would mean deciding what a batter who has faced no
        // balls strikes at, and there is no honest answer to that.
        StrikeRate = batsman.StrikeRate ?? string.Empty,
        Dismissal = batsman.OutDescription ?? string.Empty,
        IsCaptain = batsman.IsCaptain,
        IsKeeper = batsman.IsKeeper,
    };

    private static BowlingLineDto MapBowling(CricbuzzBowler bowler) => new()
    {
        Name = bowler.Name ?? string.Empty,
        Overs = bowler.Overs ?? string.Empty,
        Maidens = bowler.Maidens,
        Runs = bowler.Runs,
        Wickets = bowler.Wickets,
        Economy = bowler.Economy ?? string.Empty,
    };

    private static ExtrasDto MapExtras(CricbuzzExtras? extras) => new()
    {
        Byes = extras?.Byes ?? 0,
        LegByes = extras?.LegByes ?? 0,
        Wides = extras?.Wides ?? 0,
        NoBalls = extras?.NoBalls ?? 0,
        Penalty = extras?.Penalty ?? 0,

        // Summed rather than trusted when absent, so the row still adds up on a payload that
        // omitted the total.
        Total = extras?.Total
            ?? (extras?.Byes ?? 0) + (extras?.LegByes ?? 0) + (extras?.Wides ?? 0)
               + (extras?.NoBalls ?? 0) + (extras?.Penalty ?? 0),
    };
}

/// <summary>Supplies no scorecard, which is what happens when the source is switched off.</summary>
/// <remarks>
/// Registered in place of the real one rather than leaving the dependency nullable, so the match
/// page has one code path instead of two and the disabled case is exercised by the same tests.
/// </remarks>
internal sealed class NoMatchScorecardProvider : IMatchScorecardProvider
{
    public Task<ScorecardDto?> GetScorecardAsync(
        MatchIdentity match,
        CancellationToken cancellationToken)
        => Task.FromResult<ScorecardDto?>(null);
}
