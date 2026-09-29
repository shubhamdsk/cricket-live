using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.CricketData;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CricketLive.Infrastructure.Tests.CricketData;

/// <summary>
/// Exercised against responses captured from api.cricapi.com during the Sprint 3 spike, so the
/// provider's real inconsistencies are under test rather than an idealised version of them.
/// </summary>
public class CricketDataMatchMapperTests
{
    private static readonly IReadOnlyList<MatchDetailsDto> Matches = LoadFixture();

    private static MatchDetailsDto Match(string startsWith) =>
        Matches.Single(match => match.Slug.StartsWith(startsWith, StringComparison.Ordinal));

    [Fact]
    public void Maps_the_parts_the_provider_packs_into_one_name()
    {
        var match = Match("india-vs-west-indies");

        Assert.Equal("1st ODI", match.MatchTitle);
        Assert.Equal("West Indies tour of India, 2026", match.SeriesName);
        Assert.Equal("Greenfield International Stadium, Thiruvananthapuram", match.Venue);
        Assert.Equal("India won by 8 wkts", match.StatusText);
        Assert.Equal(MatchFormat.Odi, match.Format);
        Assert.Equal(MatchStatus.Completed, match.Status);
    }

    [Fact]
    public void Treats_the_offsetless_provider_timestamp_as_utc()
    {
        var match = Match("india-vs-west-indies");

        Assert.Equal(TimeSpan.Zero, match.StartTimeUtc.Offset);
        Assert.Equal(new DateTime(2026, 9, 27, 8, 30, 0, DateTimeKind.Utc), match.StartTimeUtc.UtcDateTime);
    }

    [Fact]
    public void Ends_the_slug_with_the_provider_id_so_a_pretty_url_still_resolves()
    {
        var match = Match("india-vs-west-indies");

        Assert.EndsWith(match.Id, match.Slug, StringComparison.Ordinal);
        Assert.Equal($"india-vs-west-indies-{match.Id}", match.Slug);
    }

    [Fact]
    public void Uses_the_provider_short_names_when_it_has_them()
    {
        var match = Match("india-vs-west-indies");

        Assert.Equal("IND", match.Home.Team.ShortName);
        Assert.Equal("WI", match.Away.Team.ShortName);
        Assert.NotNull(match.Home.Team.LogoUrl);
    }

    [Fact]
    public void Keeps_the_ball_count_in_the_over_figure()
    {
        var match = Match("india-vs-west-indies");

        Assert.Equal("41.4", Assert.Single(match.Home.Innings).Overs);
        Assert.Equal("50", Assert.Single(match.Away.Innings).Overs);
    }

    /// <summary>
    /// The case the mapper exists for. Within this one match the provider writes innings labels two
    /// different ways — "northamptonshire Inning 1" and "Middlesex,Northamptonshire Inning 1" — and
    /// leaves the team field empty, so a naive reading puts every innings on the wrong side.
    /// </summary>
    [Fact]
    public void Attributes_innings_correctly_despite_the_comma_form()
    {
        var match = Match("middlesex-vs-northamptonshire");

        Assert.Equal("Middlesex", match.Home.Team.Name);
        Assert.Equal("Northamptonshire", match.Away.Team.Name);

        Assert.Equal([255, 134], match.Home.Innings.Select(innings => innings.Runs));
        Assert.Equal([103, 285], match.Away.Innings.Select(innings => innings.Runs));
    }

    [Fact]
    public void Orders_innings_by_their_number()
    {
        var match = Match("middlesex-vs-northamptonshire");

        Assert.Equal([1, 2], match.Home.Innings.Select(innings => innings.Number));
        Assert.Equal([1, 2], match.Away.Innings.Select(innings => innings.Number));
    }

    /// <summary>
    /// Independent evidence that the attribution is right: the provider states the result in runs,
    /// and only a correct split of the innings reproduces that number.
    /// </summary>
    [Fact]
    public void Attributed_totals_reconcile_with_the_provider_result_sentence()
    {
        var match = Match("warwickshire-vs-leicestershire");

        Assert.Equal("Warwickshire won by an innings and 79 runs", match.StatusText);

        var warwickshire = match.Home.Innings.Sum(innings => innings.Runs);
        var leicestershire = match.Away.Innings.Sum(innings => innings.Runs);

        Assert.Equal(79, warwickshire - leicestershire);
    }

    [Fact]
    public void Records_a_side_that_batted_only_once_in_a_test()
    {
        var match = Match("warwickshire-vs-leicestershire");

        Assert.Single(match.Home.Innings);
        Assert.Equal(2, match.Away.Innings.Count);
    }

    [Fact]
    public void Reports_the_provider_capability_flags_rather_than_assuming_them()
    {
        Assert.All(Matches, match => Assert.False(match.HasBallByBall));
        Assert.Contains(Matches, match => match.HasSquads);
    }

    [Theory]
    [InlineData("t20", MatchFormat.T20)]
    [InlineData("odi", MatchFormat.Odi)]
    [InlineData("test", MatchFormat.Test)]
    [InlineData("TEST", MatchFormat.Test)]
    [InlineData("t10", MatchFormat.Other)]
    [InlineData(null, MatchFormat.Other)]
    public void Maps_known_formats_and_falls_back_for_the_rest(string? matchType, MatchFormat expected)
    {
        var match = Map(new CricketDataMatch
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Name = "A vs B, Only Match, Some Trophy 2026",
            MatchType = matchType,
            Teams = ["A", "B"]
        });

        Assert.Equal(expected, match!.Format);
    }

    [Theory]
    [InlineData(false, false, MatchStatus.Upcoming)]
    [InlineData(true, false, MatchStatus.Live)]
    [InlineData(true, true, MatchStatus.Completed)]
    public void Derives_status_from_the_started_and_ended_flags(
        bool started,
        bool ended,
        MatchStatus expected)
    {
        var match = Map(new CricketDataMatch
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Name = "A vs B, Only Match, Some Trophy 2026",
            Teams = ["A", "B"],
            MatchStarted = started,
            MatchEnded = ended
        });

        Assert.Equal(expected, match!.Status);
    }

    [Fact]
    public void Builds_initials_for_teams_the_provider_has_no_short_name_for()
    {
        var match = Map(new CricketDataMatch
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Name = "Northern Knights vs Munster, Only Match, Some Trophy 2026",
            Teams = ["Northern Knights", "Munster"]
        });

        Assert.Equal("NK", match!.Home.Team.ShortName);
        Assert.Equal("MUN", match.Away.Team.ShortName);
    }

    [Fact]
    public void Drops_an_innings_it_cannot_attribute_rather_than_guessing()
    {
        var match = Map(new CricketDataMatch
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Name = "A vs B, Only Match, Some Trophy 2026",
            Teams = ["Aardvarks", "Badgers"],
            Score =
            [
                new CricketDataScore { Runs = 100, Wickets = 4, Overs = 20m, Inning = "Aardvarks Inning 1" },
                new CricketDataScore { Runs = 90, Wickets = 8, Overs = 20m, Inning = "Crocodiles Inning 1" }
            ]
        });

        Assert.Single(match!.Home.Innings);
        Assert.Empty(match.Away.Innings);
    }

    [Fact]
    public void Discards_a_match_with_no_id_because_nothing_could_link_to_it()
    {
        var match = Map(new CricketDataMatch { Name = "A vs B", Teams = ["A", "B"] });

        Assert.Null(match);
    }

    [Fact]
    public void Leaves_innings_empty_for_a_match_that_has_not_started()
    {
        var match = Map(new CricketDataMatch
        {
            Id = "11111111-1111-1111-1111-111111111111",
            Name = "A vs B, Only Match, Some Trophy 2026",
            Teams = ["A", "B"],
            MatchStarted = false
        });

        Assert.Empty(match!.Home.Innings);
        Assert.Empty(match.Away.Innings);
    }

    private static MatchDetailsDto? Map(CricketDataMatch source) =>
        new CricketDataMatchMapper(NullLogger<CricketDataMatchMapper>.Instance).ToMatchDetails(source);

    private static IReadOnlyList<MatchDetailsDto> LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "currentMatches.json");
        using var stream = File.OpenRead(path);

        var envelope = JsonSerializer.Deserialize<CricketDataEnvelope<List<CricketDataMatch>>>(
            stream,
            JsonSerializerOptions.Web);

        var mapper = new CricketDataMatchMapper(NullLogger<CricketDataMatchMapper>.Instance);

        return [.. (envelope?.Data ?? []).Select(mapper.ToMatchDetails).OfType<MatchDetailsDto>()];
    }
}
