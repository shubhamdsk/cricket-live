using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Tests.Matches;

/// <summary>
/// The in-memory half of the filter. Its SQL twin is covered by
/// <c>SqlMatchArchiveTests</c>, and the two are deliberately tested against the same cases.
/// </summary>
public sealed class MatchFilterTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 15, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void An_empty_filter_accepts_everything()
    {
        Assert.True(MatchFilter.None.IsEmpty);
        Assert.True(MatchFilter.None.Matches(Match()));
    }

    [Fact]
    public void A_series_name_alone_is_not_an_empty_filter()
    {
        Assert.False(new MatchFilter(SeriesName: "Ashes").IsEmpty);
    }

    [Fact]
    public void Whitespace_is_not_a_series_filter()
    {
        // An empty text box should not exclude every match.
        var filter = new MatchFilter(SeriesName: "   ");

        Assert.True(filter.IsEmpty);
        Assert.True(filter.Matches(Match()));
    }

    [Theory]
    [InlineData(MatchStatus.Completed, true)]
    [InlineData(MatchStatus.Live, false)]
    [InlineData(MatchStatus.Upcoming, false)]
    public void Status_is_matched_exactly(MatchStatus wanted, bool expected)
    {
        var filter = new MatchFilter(Status: wanted);

        Assert.Equal(expected, filter.Matches(Match(status: MatchStatus.Completed)));
    }

    [Fact]
    public void The_lower_bound_includes_a_match_starting_on_the_instant()
    {
        Assert.True(new MatchFilter(FromUtc: Start).Matches(Match()));
    }

    [Fact]
    public void The_upper_bound_excludes_a_match_starting_on_the_instant()
    {
        // Half-open, so 15 March 00:00 to 16 March 00:00 and 16 March onwards neither overlap nor
        // leave a gap. A match starting exactly at midnight belongs to the later day only.
        Assert.False(new MatchFilter(ToUtc: Start).Matches(Match()));
    }

    [Fact]
    public void A_day_selects_the_matches_that_started_within_it()
    {
        var day = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var filter = new MatchFilter(FromUtc: day, ToUtc: day.AddDays(1));

        Assert.True(filter.Matches(Match(start: Start)));
        Assert.False(filter.Matches(Match(start: Start.AddDays(-1))));
        Assert.False(filter.Matches(Match(start: Start.AddDays(1))));
    }

    [Theory]
    [InlineData("West Indies tour of India, 2026", true)]
    [InlineData("west indies tour of india, 2026", true)]
    [InlineData("WEST INDIES TOUR OF INDIA, 2026", true)]
    [InlineData("West Indies tour of India", false)]
    [InlineData("tour of India", false)]
    public void Series_is_matched_whole_and_case_insensitively(string asked, bool expected)
    {
        // Not a substring match: "tour of India" would otherwise pull in every touring series
        // and silently widen what the reader asked for.
        var filter = new MatchFilter(SeriesName: asked);

        Assert.Equal(expected, filter.Matches(Match(series: "West Indies tour of India, 2026")));
    }

    [Fact]
    public void Every_condition_must_hold_at_once()
    {
        var filter = new MatchFilter(
            Status: MatchStatus.Completed,
            FromUtc: Start.AddHours(-1),
            ToUtc: Start.AddHours(1),
            SeriesName: "A series");

        Assert.True(filter.Matches(Match()));
        Assert.False(filter.Matches(Match(status: MatchStatus.Live)));
        Assert.False(filter.Matches(Match(series: "Another series")));
        Assert.False(filter.Matches(Match(start: Start.AddDays(3))));
    }

    private static MatchDto Match(
        MatchStatus status = MatchStatus.Completed,
        DateTimeOffset? start = null,
        string series = "A series")
    {
        var team = new TeamInningsDto(new TeamDto("india", "India", "IND", null), []);

        return new MatchDto
        {
            Id = "a",
            Slug = "a",
            Status = status,
            Format = MatchFormat.Odi,
            SeriesId = "series-1",
            SeriesName = series,
            MatchTitle = "1st ODI",
            Venue = "Somewhere",
            StartTimeUtc = start ?? Start,
            Home = team,
            Away = team,
            StatusText = string.Empty,
        };
    }
}
