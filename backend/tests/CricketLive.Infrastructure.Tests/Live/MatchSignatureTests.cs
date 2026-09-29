using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.Live;

namespace CricketLive.Infrastructure.Tests.Live;

public class MatchSignatureTests
{
    [Fact]
    public void Two_separately_built_copies_of_the_same_match_share_a_signature()
    {
        // The reason this type exists. Both copies allocate their own innings lists, so record
        // equality would call them different and wake every client on every poll.
        Assert.NotEqual(Match(), Match());
        Assert.Equal(MatchSignature.For(Match()), MatchSignature.For(Match()));
    }

    [Fact]
    public void A_run_changes_the_signature()
    {
        var before = MatchSignature.For(Match());
        var after = MatchSignature.For(Match(homeRuns: 301));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void A_wicket_changes_the_signature()
    {
        Assert.NotEqual(MatchSignature.For(Match()), MatchSignature.For(Match(homeWickets: 3)));
    }

    [Fact]
    public void A_ball_changes_the_signature()
    {
        Assert.NotEqual(MatchSignature.For(Match()), MatchSignature.For(Match(homeOvers: "41.5")));
    }

    [Fact]
    public void The_result_sentence_changes_the_signature()
    {
        // Innings can be identical while the provider decides the match is over.
        Assert.NotEqual(
            MatchSignature.For(Match()),
            MatchSignature.For(Match(statusText: "India won by 8 wkts")));
    }

    [Fact]
    public void A_new_innings_changes_the_signature()
    {
        var second = Match() with
        {
            Away = new TeamInningsDto(
                new TeamDto("west-indies", "West Indies", "WI", null),
                [new InningsScoreDto(1, 295, 7, "50"), new InningsScoreDto(2, 12, 0, "3.1")]),
        };

        Assert.NotEqual(MatchSignature.For(Match()), MatchSignature.For(second));
    }

    [Fact]
    public void Cosmetic_fields_nobody_watches_do_not_change_the_signature()
    {
        // Venue casing or a late-arriving crest should not count as a score update.
        var restyled = Match() with { Venue = "GREENFIELD INTERNATIONAL STADIUM" };

        Assert.Equal(MatchSignature.For(Match()), MatchSignature.For(restyled));
    }

    private static MatchDetailsDto Match(
        int homeRuns = 300,
        int homeWickets = 2,
        string homeOvers = "41.4",
        string statusText = "India need 5 runs") => new()
        {
            Id = "90ae280c-cb10-4d58-9bcc-ec95294819e6",
            Slug = "india-vs-west-indies-90ae280c-cb10-4d58-9bcc-ec95294819e6",
            Status = MatchStatus.Live,
            Format = MatchFormat.Odi,
            SeriesName = "West Indies tour of India, 2026",
            MatchTitle = "1st ODI",
            Venue = "Greenfield International Stadium",
            StartTimeUtc = DateTimeOffset.Parse("2026-09-27T08:30:00Z"),
            Home = new TeamInningsDto(
                new TeamDto("india", "India", "IND", null),
                [new InningsScoreDto(1, homeRuns, homeWickets, homeOvers)]),
            Away = new TeamInningsDto(
                new TeamDto("west-indies", "West Indies", "WI", null),
                [new InningsScoreDto(1, 295, 7, "50")]),
            StatusText = statusText,
            HasBallByBall = false,
            HasSquads = false,
        };
}
