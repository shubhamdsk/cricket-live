using System.Globalization;
using System.Text.RegularExpressions;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Reads the batting side and the innings number out of the provider's free-text innings label.
/// </summary>
/// <remarks>
/// <para>
/// The provider has no usable team field on a score entry, so the label is all we have, and it is
/// written two different ways within a single match:
/// <code>
/// "northamptonshire Inning 1"             -> Northamptonshire, innings 1
/// "Middlesex,Northamptonshire Inning 1"   -> Middlesex, innings 1
/// </code>
/// In the comma form the first name was the batting side in four completed County Championship
/// matches, reconciled against the provider's own result sentences.
/// </para>
/// <para>
/// <b>It is not always the batting side.</b> India v West Indies, 3rd ODI 2026, put West Indies'
/// 352/5 against India: India ended up with two first innings and West Indies with none. So the
/// comma form is a hint rather than an answer, and the second name is kept so the mapper can fall
/// back to it when the first side has already batted that innings.
/// </para>
/// </remarks>
internal static partial class InningsLabel
{
    [GeneratedRegex(@"\s+Inning\s+(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex InningsSuffix();

    public static ParsedInningsLabel Parse(string? label)
    {
        var text = label ?? string.Empty;
        var number = 1;

        var suffix = InningsSuffix().Match(text);
        if (suffix.Success)
        {
            number = int.Parse(suffix.Groups[1].ValueSpan, CultureInfo.InvariantCulture);
            text = text[..suffix.Index];
        }

        var comma = text.IndexOf(',', StringComparison.Ordinal);
        if (comma >= 0)
        {
            return new ParsedInningsLabel(text[..comma].Trim(), number, text[(comma + 1)..].Trim());
        }

        return new ParsedInningsLabel(text.Trim(), number, null);
    }
}

/// <param name="TeamName">The batting side as the label states it; the first name in the comma form.</param>
/// <param name="Number">The innings number, 1 when the label gives none.</param>
/// <param name="OtherTeamName">The second name in the comma form, or <see langword="null"/> for the plain form.</param>
internal readonly record struct ParsedInningsLabel(string TeamName, int Number, string? OtherTeamName)
{
    public bool NamesBothSides => OtherTeamName is not null;

    public void Deconstruct(out string teamName, out int number)
    {
        teamName = TeamName;
        number = Number;
    }
}
