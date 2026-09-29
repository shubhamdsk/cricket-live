using CricketLive.Infrastructure.Cricbuzz;

namespace CricketLive.Infrastructure.Tests.Cricbuzz;

/// <summary>
/// Every title in the first group is real, captured from Cricbuzz on 2026-09-29 by the spike in
/// <c>.spike/</c>. They are kept verbatim, line breaks and all, because the whitespace is part of
/// what the parser has to survive. When these start failing, the markup moved and the enrichment
/// needs revisiting — that is the point of pinning them.
/// </summary>
public class CricbuzzOgTitleTests
{
    private static string Page(string ogTitle)
        => $"""<html><head><meta property="og:title" content="{ogTitle}"><title>x</title></head></html>""";

    [Fact]
    public void A_one_day_match_in_progress_gives_both_batters()
    {
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "AUSA 97/3 (32.4) \n                 (Jason Sangha 46(74) Nathan McSweeney 32(72)) " +
            "| India A vs Australia A, 2nd unofficial Test, Australia A tour of India 2026"));

        Assert.Equal(
            [new("Jason Sangha", 46, 74), new("Nathan McSweeney", 32, 72)],
            batters);
    }

    [Fact]
    public void A_five_letter_team_abbreviation_does_not_disturb_the_batters()
    {
        // The original truncated INDWA to NDWA reading the score, because its team pattern capped
        // at four letters. Nothing here reads the team at all, so the length cannot matter.
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "INDWA 136/4 (41.5) \n                 (Anushka Sharma 11(28) Tanisha Singh 7(21)) " +
            "| India A Women vs Australia A Women, Only unofficial Test"));

        Assert.Equal(
            [new("Anushka Sharma", 11, 28), new("Tanisha Singh", 7, 21)],
            batters);
    }

    [Fact]
    public void A_test_match_with_two_innings_is_read_like_any_other()
    {
        // The original could not parse this shape at all and returned nothing for the score.
        // Batters sit in the same place regardless of how many innings precede them.
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "AUSU19 217 &amp; 85/4 vs INDU19\n                494 (Eli Brain 5(7) Blake Cattle 30(36)) " +
            "| India U19 vs Australia U19, 1st unofficial Test"));

        Assert.Equal(
            [new("Eli Brain", 5, 7), new("Blake Cattle", 30, 36)],
            batters);
    }

    [Fact]
    public void A_lone_batter_is_kept_rather_than_thrown_away()
    {
        // The original discarded both entries unless it found exactly two, so a last man standing
        // came back as placeholder text. One real batter beats two invented ones.
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "NZA 121 vs SLA\n                261 (Kristian Clarke 18(22) ) " +
            "| Sri Lanka A vs New Zealand A, 1st unofficial ODI"));

        Assert.Equal([new("Kristian Clarke", 18, 22)], batters);
    }

    [Fact]
    public void A_match_that_has_not_started_has_nobody_batting()
    {
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "Bangladesh vs Malaysia, 4th Quarter-Final, Asian Games 2026, Today, Asian Games 2026"));

        Assert.Empty(batters);
    }

    [Fact]
    public void An_over_count_is_never_mistaken_for_a_score()
    {
        // "(32.4)" is thirty-two overs and four balls. It must not become 32 runs off 4 balls.
        var batters = CricbuzzOgTitle.ReadBatters(Page("AUSA 97/3 (32.4) | India A vs Australia A"));

        Assert.Empty(batters);
    }

    [Fact]
    public void An_exact_over_count_is_not_mistaken_for_a_score_either()
    {
        // Captured an hour after the fixtures above, when the same match reached over 34 exactly and
        // Cricbuzz dropped the decimal. "104/4 (34)" is the shape closest to a batter's "34(74)",
        // and the space before the bracket is the only thing telling them apart.
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "AUSA 104/4 (34) (Nathan McSweeney 34(74) Liam Scott 5(5)) | India A vs Australia A"));

        Assert.Equal(
            [new("Nathan McSweeney", 34, 74), new("Liam Scott", 5, 5)],
            batters);
    }

    [Fact]
    public void An_apostrophe_survives_as_an_apostrophe()
    {
        // The original ran html.escape over every name, so O'Brien reached the caller as
        // O&#x27;Brien and would have rendered that way.
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "IRE 88/2 (14.2) (Kevin O&#39;Brien 40(31) Paul Stirling 30(28)) | Ireland vs Scotland"));

        Assert.Equal("Kevin O'Brien", batters[0].Name);
    }

    [Fact]
    public void A_hyphenated_name_stays_whole()
    {
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "SA 70/1 (11.0) (Jean-Paul Duminy 35(29) Temba Bavuma 30(28)) | South Africa vs India"));

        Assert.Equal("Jean-Paul Duminy", batters[0].Name);
    }

    [Fact]
    public void Nobody_beyond_the_two_at_the_crease_is_reported()
    {
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "IND 200/5 (40.0) (A One 10(10) B Two 20(20) C Three 30(30) D Four 40(40)) | India vs Sri Lanka"));

        Assert.Equal(2, batters.Count);
    }

    [Fact]
    public void Prose_after_the_pipe_cannot_contribute_a_batter()
    {
        // The series name is free text and has produced surprises before. Only the scoreline counts.
        var batters = CricbuzzOgTitle.ReadBatters(Page(
            "IND vs SL | Some Trophy featuring Ghost Player 99(99), 2026"));

        Assert.Empty(batters);
    }

    [Fact]
    public void The_meta_tag_is_found_whichever_order_its_attributes_are_in()
    {
        var reversed = """<html><head><meta content="AUS 50/1 (8.0) (Sam Konstas 25(20) Usman Khawaja 20(28)) | Australia vs England" property="og:title"></head></html>""";

        Assert.Equal(
            [new("Sam Konstas", 25, 20), new("Usman Khawaja", 20, 28)],
            CricbuzzOgTitle.ReadBatters(reversed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><head><title>no meta tag here</title></head></html>")]
    [InlineData("<html>not even html, just words</html>")]
    public void Anything_unreadable_yields_nobody_rather_than_throwing(string? html)
        => Assert.Empty(CricbuzzOgTitle.ReadBatters(html));
}
