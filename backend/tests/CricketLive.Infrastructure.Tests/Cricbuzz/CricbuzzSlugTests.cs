using CricketLive.Application.Enrichment;
using CricketLive.Infrastructure.Cricbuzz;

namespace CricketLive.Infrastructure.Tests.Cricbuzz;

/// <summary>
/// Every slug here is real, taken from Cricbuzz's own listing pages on 2026-09-29 by the spike in
/// <c>.spike/</c>. The listing held 26 matches; the awkward ones are all represented.
/// </summary>
public class CricbuzzSlugTests
{
    /// <summary>The real listing, trimmed to the cases that decide the design.</summary>
    private static readonly (string Id, string Slug)[] RealListing =
    [
        ("155422", "ausa-vs-inda-2nd-unofficial-test-australia-a-tour-of-india-2026"),
        ("151543", "ind-vs-wi-2nd-odi-west-indies-tour-of-india-2026"),
        ("151554", "ind-vs-wi-3rd-odi-west-indies-tour-of-india-2026"),
        ("171040", "sl-vs-nep-3rd-quarter-final-asian-games-2026"),
        ("171060", "ind-vs-sl-2nd-semi-final-asian-games-2026"),
        ("171061", "tbc-vs-tbc-bronze-medal-match-asian-games-2026"),
        ("171070", "tbc-vs-tbc-final-asian-games-2026"),
        // Same title and series, different teams. The only collision in the real listing.
        ("173031", "bor-vs-nwest-pool-a-csa-t20-challenge-2026"),
        ("173133", "war-vs-limpo-pool-a-csa-t20-challenge-2026"),
        ("147909", "rsa-vs-aus-3rd-odi-australia-tour-of-south-africa-2026"),
    ];

    private static IEnumerable<CricbuzzListing> Listing() =>
        RealListing.Select(entry => CricbuzzSlug.Read(entry.Id, entry.Slug)).OfType<CricbuzzListing>();

    private static MatchIdentity Match(
        string title,
        string series,
        string home = "IND",
        string away = "WI") => new("ours", title, series, home, away);

    [Fact]
    public void Slugifying_our_fields_reproduces_their_tail()
    {
        // The entire premise. CricketData writes "West Indies tour of India, 2026" and Cricbuzz
        // writes "west-indies-tour-of-india-2026" — the same string once punctuation is dropped.
        Assert.Equal(
            "2nd-odi-west-indies-tour-of-india-2026",
            CricbuzzSlug.Slugify("2nd ODI", "West Indies tour of India, 2026"));
    }

    [Fact]
    public void A_match_in_the_listing_resolves_to_its_id()
    {
        var resolved = CricbuzzSlug.Resolve(
            Match("2nd ODI", "West Indies tour of India, 2026"),
            Listing());

        Assert.Equal("151543", resolved);
    }

    [Fact]
    public void The_match_that_burned_us_on_teams_alone_now_declines()
    {
        // This is the case that justified the whole design. Our IND v WI 1st ODI matched two
        // entries on teams alone — the 2nd and 3rd ODIs, both wrong, because the 1st had already
        // dropped off the listing. Keying on the title finds nothing, which is the truth.
        Assert.Null(CricbuzzSlug.Resolve(
            Match("1st ODI", "West Indies tour of India, 2026"),
            Listing()));
    }

    [Fact]
    public void Two_matches_sharing_a_title_are_separated_by_their_teams()
    {
        // Both are "Pool A" of the same tournament, so only the teams tell them apart.
        Assert.Equal(
            "173133",
            CricbuzzSlug.Resolve(
                Match("Pool A", "CSA T20 Challenge 2026", home: "WAR", away: "LIMPO"),
                Listing()));
    }

    [Fact]
    public void A_shared_title_with_unrecognised_teams_declines_rather_than_picking_one()
    {
        // Two candidates, and the teams match neither. One of them is not a 50/50 guess worth
        // taking; it is a coin flip presented to the reader as a fact.
        Assert.Null(CricbuzzSlug.Resolve(
            Match("Pool A", "CSA T20 Challenge 2026", home: "XYZ", away: "ABC"),
            Listing()));
    }

    [Fact]
    public void A_fixture_with_undecided_teams_is_not_a_candidate()
    {
        // Cricbuzz lists finals before the finalists are known. Enriching one would mean showing
        // batters for a match whose teams we cannot even confirm.
        Assert.Null(CricbuzzSlug.Read("171070", "tbc-vs-tbc-final-asian-games-2026"));
        Assert.Null(CricbuzzSlug.Resolve(Match("Final", "Asian Games 2026"), Listing()));
    }

    [Fact]
    public void The_same_match_number_in_two_series_resolves_to_two_different_matches()
    {
        // "3rd ODI" appears twice in the real listing, under different tours. A match number is
        // only meaningful inside its series, which is why the key carries both.
        var listing = Listing().ToArray();

        Assert.Equal(
            "151554",
            CricbuzzSlug.Resolve(Match("3rd ODI", "West Indies tour of India, 2026"), listing));

        Assert.Equal(
            "147909",
            CricbuzzSlug.Resolve(
                Match("3rd ODI", "Australia tour of South Africa, 2026", home: "RSA", away: "AUS"),
                listing));
    }

    [Fact]
    public void A_match_number_that_does_not_exist_in_its_series_declines()
    {
        // The Asian Games listing has quarter-finals and semi-finals, no ODIs.
        Assert.Null(CricbuzzSlug.Resolve(Match("3rd ODI", "Asian Games 2026"), Listing()));
    }

    [Fact]
    public void Team_order_does_not_matter()
    {
        // Cricbuzz lists this one as ausa-vs-inda while CricketData has India at home.
        Assert.Equal(
            "155422",
            CricbuzzSlug.Resolve(
                Match("2nd unofficial Test", "Australia A tour of India 2026", home: "INDA", away: "AUSA"),
                Listing()));
    }

    [Theory]
    [InlineData("ind-vs-wi")]            // teams but no title or series to key on
    [InlineData("vs-wi-1st-odi-series")] // nothing before "vs"
    [InlineData("ind-vs-wi-")]           // trailing separator, still nothing after the teams
    [InlineData("just-a-series-name")]   // no "vs" at all
    public void A_slug_that_cannot_be_a_fixture_link_is_ignored(string slug)
        => Assert.Null(CricbuzzSlug.Read("1234", slug));

    [Fact]
    public void A_slug_shaped_like_a_fixture_is_accepted_even_if_it_is_nonsense()
    {
        // Nothing distinguishes "no-vs-token-here" from a real fixture structurally, and being
        // strict here would mean guessing. It is harmless: resolution needs an exact tail match, so
        // a junk entry can only fail to match, never match the wrong thing.
        Assert.NotNull(CricbuzzSlug.Read("1234", "no-vs-token-here-at-all"));
        Assert.Null(CricbuzzSlug.Resolve(Match("1st ODI", "Some Series 2026"), Listing()));
    }

    [Fact]
    public void A_match_with_no_title_or_series_never_resolves()
    {
        // An empty key would match every listing whose tail is also empty, which is exactly the
        // kind of accident that ends in the wrong scores on screen.
        Assert.Null(CricbuzzSlug.Resolve(Match(string.Empty, string.Empty), Listing()));
    }

    [Fact]
    public void Punctuation_and_casing_differences_do_not_prevent_a_match()
    {
        Assert.Equal(
            "147909",
            CricbuzzSlug.Resolve(
                Match("3rd ODI", "Australia Tour of South Africa, 2026", home: "RSA", away: "AUS"),
                Listing()));
    }

    [Fact]
    public void Every_tail_in_the_real_listing_parses()
    {
        // Two tbc fixtures are rejected by design; everything else must be readable.
        Assert.Equal(RealListing.Length - 2, Listing().Count());
    }
}
