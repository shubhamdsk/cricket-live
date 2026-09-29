using CricketLive.Infrastructure.Cricbuzz;

namespace CricketLive.Infrastructure.Tests.Cricbuzz;

/// <summary>
/// Read against a captured Ranji Trophy Elite 2026-27 points table, which is a real page and a
/// deliberately awkward one: four groups at once, no tie column, and every row carrying an
/// expandable per-match breakdown that must not be mistaken for more rows.
/// </summary>
public sealed class CricbuzzPointsTableTests
{
    private static string Fixture()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cricbuzz-points-table.html"));

    [Fact]
    public void Every_group_is_kept_apart()
    {
        var rows = CricbuzzPointsTable.Parse(Fixture());

        Assert.Equal(
            ["Elite Group A", "Elite Group B", "Elite Group C", "Elite Group D"],
            rows.Select(row => row.Group).Distinct().OrderBy(group => group));
    }

    [Fact]
    public void A_rows_expandable_detail_is_not_read_as_more_rows()
    {
        var rows = CricbuzzPointsTable.Parse(Fixture());

        // Each row carries a nested table of its own fixtures. Counting those as standings would
        // multiply every group several times over, so the count is the assertion that matters.
        foreach (var group in rows.GroupBy(row => row.Group))
        {
            Assert.Equal(8, group.Count());
        }
    }

    [Fact]
    public void Teams_are_not_repeated_within_a_group()
    {
        var rows = CricbuzzPointsTable.Parse(Fixture());

        foreach (var group in rows.GroupBy(row => row.Group))
        {
            Assert.Equal(group.Count(), group.Select(row => row.TeamName).Distinct().Count());
        }
    }

    [Fact]
    public void A_competition_without_a_tie_column_reports_no_ties_rather_than_misreading_one()
    {
        var rows = CricbuzzPointsTable.Parse(Fixture());

        // The Ranji table publishes P, W, L, NR, Pts and NRR. If the parser assumed a fixed column
        // set, the missing tie column would shift every value after it by one.
        Assert.All(rows, row => Assert.Equal(0, row.Tied));
    }

    [Fact]
    public void Played_is_never_less_than_the_results_that_make_it_up()
    {
        var rows = CricbuzzPointsTable.Parse(Fixture());

        Assert.NotEmpty(rows);

        // Independent of how the columns were mapped: if points landed in the won column, or
        // wins in played, this stops agreeing.
        Assert.All(rows, row => Assert.True(
            row.Won + row.Lost + row.Tied + row.NoResult <= row.Played,
            $"{row.Group} {row.TeamName}: {row.Won}+{row.Lost}+{row.Tied}+{row.NoResult} > {row.Played}"));
    }

    [Fact]
    public void Net_run_rate_is_kept_as_written()
    {
        var rows = CricbuzzPointsTable.Parse(Fixture());

        // Signed and to three places. Kept as text so a negative rate is not rounded away or
        // reformatted into something the source never said.
        Assert.All(rows, row => Assert.Matches(@"^-?\d+\.\d+$", row.NetRunRate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body><p>Nothing resembling a table.</p></body></html>")]
    public void Anything_unreadable_is_no_table_rather_than_a_wrong_one(string? html)
    {
        Assert.Empty(CricbuzzPointsTable.Parse(html));
    }

    [Fact]
    public void Rows_with_no_header_before_them_are_ignored()
    {
        // Markup could change so the header is no longer recognisable. That must produce nothing,
        // not rows whose columns are guessed.
        const string Html = """
            <div class="grid point-table-grid"><div>-</div><div>MUM</div><div>5</div><div>3</div></div>
            """;

        Assert.Empty(CricbuzzPointsTable.Parse(Html));
    }

    [Fact]
    public void A_row_whose_numbers_do_not_parse_is_dropped_rather_than_zeroed()
    {
        const string Html = """
            <div class="grid point-table-grid"><div>&nbsp;</div><div>Group A</div><div>P</div><div>W</div><div>L</div><div>Pts</div><div>NRR</div></div>
            <div class="grid point-table-grid"><div>-</div><div>MUM</div><div>5</div><div>3</div><div>2</div><div>12</div><div>0.500</div></div>
            <div class="grid point-table-grid"><div>-</div><div>DEL</div><div>five</div><div>3</div><div>2</div><div>12</div><div>0.500</div></div>
            """;

        var rows = CricbuzzPointsTable.Parse(Html);

        // A zero here would be a claim that Delhi played no matches, which is not what the page
        // said. Absence is the only honest reading of a cell we could not understand.
        Assert.Equal(["MUM"], rows.Select(row => row.TeamName));
    }
}
