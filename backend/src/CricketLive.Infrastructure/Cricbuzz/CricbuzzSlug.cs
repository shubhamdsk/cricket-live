using System.Text.RegularExpressions;
using CricketLive.Application.Enrichment;

namespace CricketLive.Infrastructure.Cricbuzz;

/// <summary>One match as it appears in a Cricbuzz listing link.</summary>
/// <param name="Teams">The two abbreviations either side of "vs", in listing order.</param>
/// <param name="Tail">Everything after them: the match title and the series name.</param>
internal sealed record CricbuzzListing(string MatchId, IReadOnlyList<string> Teams, string Tail);

/// <summary>
/// Pairs one of our matches with a Cricbuzz match id, using nothing but the text both sides
/// already publish.
/// </summary>
/// <remarks>
/// <para>
/// Cricbuzz links read <c>/live-cricket-scores/151543/ind-vs-wi-2nd-odi-west-indies-tour-of-india-2026</c>.
/// The tail of that slug is the match title and series name, and slugifying CricketData's own
/// <c>matchTitle</c> and <c>seriesName</c> reproduces it character for character. So this is an
/// exact match on a composite key, not a similarity score.
/// </para>
/// <para>
/// <b>The team pair alone is not a key, and using it would show wrong scores.</b> Measured against
/// a real listing of 26 matches: <c>ind</c> appeared on three, <c>skr</c> on four, and two fixtures
/// were <c>tbc-vs-tbc</c>. Our "IND v WI, 1st ODI" matched two Cricbuzz entries on teams alone —
/// the 2nd and 3rd ODIs, both wrong, because the 1st had already dropped off the listing. A join
/// like that does not fail; it quietly puts another match's batters on the page.
/// </para>
/// <para>
/// Hence the last rule, which is the important one: a unique answer or no answer. Declining costs
/// two player names. Guessing tells the reader something false.
/// </para>
/// </remarks>
internal static partial class CricbuzzSlug
{
    /// <summary>Cricbuzz's placeholder for a fixture whose teams are not decided.</summary>
    private const string Undecided = "tbc";

    public static string Slugify(params string[] parts)
        => NonSlug().Replace(string.Join(' ', parts).ToLowerInvariant(), "-").Trim('-');

    /// <summary>Splits a listing slug into its team pair and its title-and-series tail.</summary>
    public static CricbuzzListing? Read(string matchId, string slug)
    {
        var parts = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var pivot = Array.IndexOf(parts, "vs");

        // Needs an abbreviation on each side of "vs" and something after them to key on. A slug
        // that does not look like this is not a fixture link we understand.
        if (pivot < 1 || pivot + 2 >= parts.Length)
        {
            return null;
        }

        string[] teams = [parts[pivot - 1], parts[pivot + 1]];

        if (teams.Any(team => team.Equals(Undecided, StringComparison.Ordinal)))
        {
            return null;
        }

        return new CricbuzzListing(matchId, teams, string.Join('-', parts[(pivot + 2)..]));
    }

    /// <summary>
    /// The Cricbuzz id for this match, or <see langword="null"/> when the listing does not identify
    /// exactly one.
    /// </summary>
    public static string? Resolve(MatchIdentity match, IEnumerable<CricbuzzListing> listings)
    {
        var tail = Slugify(match.MatchTitle, match.SeriesName);

        if (tail.Length == 0)
        {
            return null;
        }

        var candidates = listings
            .Where(listing => string.Equals(listing.Tail, tail, StringComparison.Ordinal))
            .ToArray();

        if (candidates.Length == 1)
        {
            return candidates[0].MatchId;
        }

        if (candidates.Length == 0)
        {
            return null;
        }

        // Group stages collide: several fixtures share a title like "Pool A" within one series. The
        // teams separate those, and are only consulted here because the two providers do not always
        // abbreviate alike — demanding agreement up front would reject matches that are fine.
        var byTeams = candidates
            .Where(listing => listing.Teams.Contains(Slugify(match.HomeShortName), StringComparer.Ordinal)
                && listing.Teams.Contains(Slugify(match.AwayShortName), StringComparer.Ordinal))
            .ToArray();

        return byTeams.Length == 1 ? byTeams[0].MatchId : null;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
