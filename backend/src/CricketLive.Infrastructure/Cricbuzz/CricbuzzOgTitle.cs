using System.Net;
using System.Text.RegularExpressions;
using CricketLive.Application.Enrichment;

namespace CricketLive.Infrastructure.Cricbuzz;

/// <summary>
/// Reads the batters at the crease out of a scorecard page's <c>og:title</c> meta tag.
/// </summary>
/// <remarks>
/// <para>
/// This is a port of the parsing in mskian/live-cricket-score-api, narrowed to the one field that
/// survived testing against real pages and corrected in three places. The spike that produced that
/// evidence is in <c>.spike/</c>; the captured titles it found are the fixtures in this parser's tests.
/// </para>
/// <para>
/// <b>Only the meta tag is read, never the page body.</b> That is the important constraint. The
/// original took the bowler from a regex over the whole document's text and consequently returned
/// names like "Tushar Deshpande View match performance View profile" on every single match, because
/// link labels sit next to the name in the flattened text. A meta tag has no such neighbours. Any
/// field that cannot be taken from <c>og:title</c> belongs to the primary provider, not here.
/// </para>
/// <para>
/// Nothing in here is stable. It reads a presentation detail of someone else's HTML, so the tests
/// are the early warning: when they fail, the markup moved.
/// </para>
/// </remarks>
internal static partial class CricbuzzOgTitle
{
    /// <summary>Only two batters can be at the crease, whatever the text appears to offer.</summary>
    private const int AtTheCrease = 2;

    public static IReadOnlyList<BatterDto> ReadBatters(string? html)
    {
        var title = ExtractTitle(html);
        if (title.Length == 0)
        {
            return [];
        }

        // Everything after the first pipe is the fixture and series name, which is prose and can
        // contain anything. Batters are only ever named before it.
        var pipe = title.IndexOf('|');
        var scoreline = pipe < 0 ? title : title[..pipe];

        var batters = new List<BatterDto>(AtTheCrease);

        foreach (Match match in BatterPattern().Matches(scoreline))
        {
            // The name is greedy over whitespace, so it arrives with the previous token's spacing
            // and any line breaks Cricbuzz wrapped the tag across.
            var name = Collapse(match.Groups["name"].Value);

            // A bare number cannot be parsed into a name, and an empty one means the pattern latched
            // onto a score fragment rather than a player.
            if (name.Length == 0)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["runs"].Value, out var runs) ||
                !int.TryParse(match.Groups["balls"].Value, out var balls))
            {
                continue;
            }

            batters.Add(new BatterDto(name, runs, balls));

            if (batters.Count == AtTheCrease)
            {
                break;
            }
        }

        return batters;
    }

    private static string ExtractTitle(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var tag = TitlePattern().Match(html);

        // Decoded because the tag holds an HTML attribute: an apostrophe arrives as &#39; and the
        // two-innings separator as &amp;. The original escaped instead of decoding, turning every
        // O'Brien into O&#x27;Brien on the way out.
        return tag.Success ? WebUtility.HtmlDecode(tag.Groups["content"].Value) : string.Empty;
    }

    private static string Collapse(string value)
        => WhitespacePattern().Replace(value, " ").Trim();

    /// <summary>
    /// The <c>og:title</c> meta tag, with either attribute order, since nothing guarantees one.
    /// </summary>
    [GeneratedRegex(
        """<meta[^>]*?property=["']og:title["'][^>]*?content=["'](?<content>[^"']*)["']|<meta[^>]*?content=["'](?<content>[^"']*)["'][^>]*?property=["']og:title["']""",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex TitlePattern();

    /// <summary>
    /// A player's name followed by their score, as in <c>Jason Sangha 46(74)</c>.
    /// </summary>
    /// <remarks>
    /// The name excludes digits, which is what keeps it from swallowing the team abbreviation and
    /// total that precede it: in <c>INDU19 494 (Eli Brain 5(7)…</c> the match can only begin at "Eli".
    /// The score requires <c>digits(digits)</c>, so an over count like <c>(32.4)</c> cannot be
    /// mistaken for one.
    /// </remarks>
    [GeneratedRegex(
        @"(?<name>[A-Za-z][A-Za-z\s.'\-]*?)\s+(?<runs>\d+)\((?<balls>\d+)\)",
        RegexOptions.ExplicitCapture)]
    private static partial Regex BatterPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
