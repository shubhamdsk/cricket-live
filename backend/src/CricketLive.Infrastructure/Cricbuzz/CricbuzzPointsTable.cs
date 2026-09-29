using System.Net;
using System.Text.RegularExpressions;
using CricketLive.Application.Series.Dtos;

namespace CricketLive.Infrastructure.Cricbuzz;

/// <summary>
/// Reads a points table out of a Cricbuzz series page.
/// </summary>
/// <remarks>
/// <para>
/// This parses page markup, which means it will break without warning when that markup changes.
/// The tests against captured pages are the alarm, and the failure mode is chosen to be harmless:
/// anything this cannot read with confidence becomes an empty list, which the UI renders as no
/// section at all. A half-read table would rank teams wrongly, which is worse than no table.
/// </para>
/// <para>
/// <b>The header is read rather than assumed.</b> Column sets differ by competition — the Ranji
/// Trophy publishes P, W, L, NR, Pts and NRR with no tie column, and limited-overs tournaments
/// commonly add one. Reading positions from the header means a competition with a different set
/// is handled rather than silently misaligned, which is the failure that would put a team's tie
/// count in its points column.
/// </para>
/// </remarks>
internal static partial class CricbuzzPointsTable
{
    /// <summary>The class on every row and header of the grid, both of which carry it.</summary>
    private const string RowMarker = "point-table-grid";

    /// <summary>Header cells we understand. Anything else in the header is ignored.</summary>
    private static readonly string[] KnownColumns = ["P", "W", "L", "T", "NR", "Pts", "NRR"];

    public static IReadOnlyList<StandingDto> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var blocks = Blocks(html).ToArray();
        var rows = new List<StandingDto>();

        string[] columns = [];
        var group = string.Empty;

        foreach (var block in blocks)
        {
            var cells = Cells(block);

            if (cells.Length < 2)
            {
                continue;
            }

            // A header names its columns; a row fills them. "Pts" in the cells is what tells the
            // two apart, and it is also the column without which a points table is not one.
            if (cells.Contains("Pts", StringComparer.Ordinal))
            {
                group = cells[0];
                columns = [.. cells[1..].Where(cell => KnownColumns.Contains(cell, StringComparer.Ordinal))];
                continue;
            }

            if (columns.Length == 0)
            {
                // Rows before any header belong to no table we can describe.
                continue;
            }

            if (TryRow(cells, columns, group, out var standing))
            {
                rows.Add(standing);
            }
        }

        return rows;
    }

    /// <summary>
    /// One row, or nothing when the cells do not line up with the header.
    /// </summary>
    /// <remarks>
    /// The first cell is a movement indicator and the second is the team, after which the values
    /// follow in header order. Every numeric column must actually parse: a row that fails is
    /// dropped rather than defaulted to zero, because a zero here is a claim about a result.
    /// </remarks>
    private static bool TryRow(string[] cells, string[] columns, string group, out StandingDto standing)
    {
        standing = null!;

        // Movement, team, then one value per column.
        if (cells.Length < 2 + columns.Length)
        {
            return false;
        }

        var team = cells[1];
        var values = cells[2..(2 + columns.Length)];

        if (string.IsNullOrWhiteSpace(team))
        {
            return false;
        }

        var byColumn = columns
            .Select((name, index) => (name, value: values[index]))
            .ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal);

        if (!TryCount(byColumn, "P", out var played)
            || !TryCount(byColumn, "W", out var won)
            || !TryCount(byColumn, "L", out var lost)
            || !TryCount(byColumn, "Pts", out var points))
        {
            return false;
        }

        // Absent is genuinely zero for these: a competition without a tie column has no ties.
        TryCount(byColumn, "T", out var tied);
        TryCount(byColumn, "NR", out var noResult);

        standing = new StandingDto
        {
            Group = group,
            TeamName = team,
            Played = played,
            Won = won,
            Lost = lost,
            Tied = tied,
            NoResult = noResult,
            Points = points,
            NetRunRate = byColumn.GetValueOrDefault("NRR", string.Empty),
        };

        return true;
    }

    private static bool TryCount(Dictionary<string, string> row, string column, out int value)
    {
        value = 0;

        return row.TryGetValue(column, out var text) && int.TryParse(text, out value);
    }

    /// <summary>Each header or row in the grid, as the markup between one marker and the next.</summary>
    private static IEnumerable<string> Blocks(string html)
    {
        var index = html.IndexOf(RowMarker, StringComparison.Ordinal);

        while (index >= 0)
        {
            var next = html.IndexOf(RowMarker, index + RowMarker.Length, StringComparison.Ordinal);
            var end = next < 0 ? html.Length : next;

            // Past the rest of the opening tag, so the block starts at the first cell.
            var open = html.IndexOf('>', index);

            if (open > 0 && open < end)
            {
                yield return html[(open + 1)..end];
            }

            index = next;
        }
    }

    /// <summary>
    /// The text of each cell in a block, in order, with empties dropped.
    /// </summary>
    /// <remarks>
    /// Empties are dropped because the team cell wraps its flag in nested elements and so emits
    /// blanks that a header cell does not. Keeping them would make header and row disagree about
    /// which position a column sits at, which is the misalignment this whole class exists to
    /// avoid.
    /// </remarks>
    private static string[] Cells(string block)
    {
        var text = CloseTag().Replace(block, "\u001f");
        text = AnyTag().Replace(text, string.Empty);

        return
        [
            .. text
                .Split('\u001f')
                .Select(cell => WebUtility.HtmlDecode(cell).Replace('\u00a0', ' ').Trim())
                .Where(cell => cell.Length > 0)
        ];
    }

    [GeneratedRegex("</div>", RegexOptions.IgnoreCase)]
    private static partial Regex CloseTag();

    [GeneratedRegex("<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex AnyTag();
}
