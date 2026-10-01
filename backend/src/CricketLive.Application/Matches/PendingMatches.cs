using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;

namespace CricketLive.Application.Matches;

/// <summary>
/// Joins the match index to the series fixture lists, so a match the index merely names comes back
/// as a full match.
/// </summary>
/// <remarks>
/// <para>
/// Neither source is any use alone here. The index knows <i>which</i> matches are imminent but
/// writes a team as <c>"India [IND]"</c> and carries no venue; the fixture lists know everything
/// about a match but have no idea which series is worth asking about. Three steps:
/// </para>
/// <list type="number">
/// <item>the index names the matches that have not finished, and the series each belongs to</item>
/// <item>the series index turns those series <i>names</i> into ids, which the match index lacks</item>
/// <item>the fixture list for each of those series supplies the matches, matched back by id</item>
/// </list>
/// <para>
/// <b>The index defines the window; the fixture lists only furnish it.</b> A fixture list is the
/// whole season, so taking everything unplayed from it would put all 31 Sheffield Shield fixtures
/// on a page asking what is coming up. Matching back by id keeps the answer to what the provider
/// itself considers current, which is the question being asked.
/// </para>
/// <para>
/// The cost is one index call plus one call per distinct active series, all cached, and in practice
/// that second number is two or three because few series are being played at once. It is capped
/// regardless, because "few series at once" is an observation about cricket and not a guarantee
/// from the provider.
/// </para>
/// </remarks>
public sealed class PendingMatches(
    IMatchIndex index,
    ISeriesIndex seriesIndex,
    ISeriesFixtures fixtures) : IPendingMatches
{
    /// <summary>
    /// How many distinct series this will fetch fixtures for in one pass.
    /// </summary>
    /// <remarks>
    /// Six, against the two or three a normal day produces, so the cap is a bound on a bad day
    /// rather than a limit on a normal one. The allowance is a hundred calls wide and the cached
    /// answers are shared by every visitor, so the real exposure is six calls every few hours.
    /// </remarks>
    private const int SeriesLimit = 6;

    public async Task<IReadOnlyList<MatchDetailsDto>> GetAsync(CancellationToken cancellationToken)
    {
        var pending = await index.GetAsync(cancellationToken);

        var wanted = pending
            .Where(entry => !entry.IsFinished)
            .ToArray();

        if (wanted.Length == 0)
        {
            return [];
        }

        var ids = wanted.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seriesIds = await ResolveAsync(wanted, cancellationToken);

        var found = new Dictionary<string, MatchDetailsDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var seriesId in seriesIds)
        {
            foreach (var match in await fixtures.GetAsync(seriesId, cancellationToken))
            {
                if (ids.Contains(match.Id))
                {
                    found[match.Id] = match;
                }
            }
        }

        return [.. found.Values.OrderBy(match => match.StartTimeUtc)];
    }

    /// <summary>
    /// The series ids behind the names the index gave, in the order the index mentioned them.
    /// </summary>
    /// <remarks>
    /// Matched on the name because that is all there is to match on — the match index carries no
    /// series id. An exact, case-insensitive comparison, deliberately: the series index lists
    /// "Sri Lanka tour of West Indies 2026" and "Sri Lanka tour of West Indies, 2026" as separate
    /// series, so anything looser than exact would pick whichever came first and be confidently
    /// wrong about which season's fixtures it was showing.
    ///
    /// Order is preserved so the cap falls on the series the index mentioned last rather than on
    /// an arbitrary one, and a name with no match in the series index contributes nothing. That is
    /// a silent loss, and the alternative — building a match out of the index's own thin fields —
    /// would put a row with no venue and a team called "India [IND]" beside complete ones.
    /// </remarks>
    private async Task<IReadOnlyList<string>> ResolveAsync(
        IReadOnlyList<MatchIndexEntry> wanted,
        CancellationToken cancellationToken)
    {
        var listed = await seriesIndex.GetAsync(cancellationToken);

        if (listed.Count == 0)
        {
            return [];
        }

        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in listed)
        {
            // First listing wins. The index returns pages newest-first, so for the duplicate names
            // above that is the more recent of the two, which is the one a current match belongs to.
            _ = byName.TryAdd(entry.Name, entry.Id);
        }

        return
        [
            .. wanted
                .Select(entry => entry.SeriesName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => byName.GetValueOrDefault(name))
                .OfType<string>()
                .Take(SeriesLimit)
        ];
    }
}
