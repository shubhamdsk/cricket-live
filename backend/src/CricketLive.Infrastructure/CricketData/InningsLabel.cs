using System.Globalization;
using System.Text.RegularExpressions;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Reads the batting side and the innings number out of the provider's free-text innings label.
/// </summary>
/// <remarks>
/// The provider has no usable team field on a score entry, so the label is all we have, and it is
/// written two different ways within a single match:
/// <code>
/// "northamptonshire Inning 1"             -> Northamptonshire, innings 1
/// "Middlesex,Northamptonshire Inning 1"   -> Middlesex, innings 1
/// </code>
/// In the comma form the batting side is the first name. That was confirmed against four completed
/// County Championship matches by reconciling each innings total against the provider's own result
/// sentence, for example Warwickshire's 372 against Leicestershire's 125 and 168 giving the reported
/// "won by an innings and 79 runs".
/// </remarks>
internal static partial class InningsLabel
{
    [GeneratedRegex(@"\s+Inning\s+(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex InningsSuffix();

    public static (string TeamName, int Number) Parse(string? label)
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
            text = text[..comma];
        }

        return (text.Trim(), number);
    }
}
