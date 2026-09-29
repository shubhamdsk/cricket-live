using CricketLive.Infrastructure.CricketData;

namespace CricketLive.Infrastructure.Tests.CricketData;

public class InningsLabelTests
{
    [Theory]
    [InlineData("northamptonshire Inning 1", "northamptonshire", 1)]
    [InlineData("West Indies Inning 1", "West Indies", 1)]
    [InlineData("surrey Inning 2", "surrey", 2)]
    public void Parse_reads_the_team_and_number_from_the_plain_form(
        string label,
        string expectedTeam,
        int expectedNumber)
    {
        var (team, number) = InningsLabel.Parse(label);

        Assert.Equal(expectedTeam, team);
        Assert.Equal(expectedNumber, number);
    }

    [Theory]
    [InlineData("Middlesex,Northamptonshire Inning 1", "Middlesex", 1)]
    [InlineData("Hampshire,Sussex Inning 2", "Hampshire", 2)]
    [InlineData("Somerset,Surrey Inning 1", "Somerset", 1)]
    public void Parse_takes_the_batting_side_from_the_comma_form(
        string label,
        string expectedTeam,
        int expectedNumber)
    {
        var (team, number) = InningsLabel.Parse(label);

        Assert.Equal(expectedTeam, team);
        Assert.Equal(expectedNumber, number);
    }

    [Fact]
    public void Parse_defaults_to_the_first_innings_when_no_number_is_given()
    {
        var (team, number) = InningsLabel.Parse("Kent");

        Assert.Equal("Kent", team);
        Assert.Equal(1, number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_survives_a_missing_label(string? label)
    {
        var (team, number) = InningsLabel.Parse(label);

        Assert.Equal(string.Empty, team);
        Assert.Equal(1, number);
    }
}
