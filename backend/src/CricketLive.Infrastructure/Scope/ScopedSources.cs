using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Scope;
using CricketLive.Application.Series;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.Scope;

/// <summary>
/// Decorators that narrow each source of cricket to what <see cref="CricketScope"/> covers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Filtering at the sources rather than at the pages.</b> Every list on the site is built from
/// five places — the live window, the match index, the series index, per-series fixtures and the
/// archive — and the pages combine them in ways that would each need their own filter: the series
/// page merges three sources by id, the teams page derives sides from matches, search reads the
/// other services' output. Narrowing the sources instead means no page knows this exists, and a
/// page added later is covered without anyone remembering to filter it.
/// </para>
/// <para>
/// The archive is the one source not here, because it filters in SQL — an in-memory pass over a
/// page of results would break the counts and the paging around them. Same policy, two
/// mechanisms, for the same reason <c>MatchFilter</c> has both.
/// </para>
/// <para>
/// Exclusions are logged at debug, not information. On a normal day this drops most of what the
/// provider sends, so at information it would be the log rather than an entry in it. Debug is
/// enough to answer "why is this match missing" on a local run, which is the only question it is
/// there to answer.
/// </para>
/// </remarks>
internal sealed class ScopedCricketDataProvider(
    ICricketDataProvider inner,
    ILogger<ScopedCricketDataProvider> logger) : ICricketDataProvider
{
    public async Task<IReadOnlyList<MatchDetailsDto>> GetCurrentMatchesAsync(
        CancellationToken cancellationToken)
    {
        var window = await inner.GetCurrentMatchesAsync(cancellationToken);

        var covered = window.Where(Covered).ToArray();

        if (covered.Length != window.Count)
        {
            logger.LogDebug(
                "Scope kept {Kept} of {Total} match(es) in the provider's window",
                covered.Length,
                window.Count);
        }

        return covered;
    }

    /// <remarks>
    /// A match outside the scope has no page, so this answers as the provider would for an id it
    /// has never heard of. Leaving it reachable would mean the site hid a match from every list
    /// and then served it anyway to anyone holding the address, which is not a coherent answer to
    /// "we do not cover this".
    /// </remarks>
    public async Task<MatchDetailsDto?> GetMatchAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        var match = await inner.GetMatchAsync(matchId, cancellationToken);

        return match is not null && Covered(match) ? match : null;
    }

    private bool Covered(MatchDetailsDto match)
    {
        if (CricketScope.IncludesMatch(match.SeriesName, match.Home.Team.Name, match.Away.Team.Name))
        {
            return true;
        }

        logger.LogDebug(
            "Match {MatchId} is out of scope: {Reason}",
            match.Id,
            CricketScope.Unrecognised(match.SeriesName, match.Home.Team.Name, match.Away.Team.Name));

        return false;
    }
}

/// <summary>
/// Narrows the match index, which is what decides whose fixtures get fetched.
/// </summary>
/// <remarks>
/// <para>
/// Judged on the series name, because an index entry carries no teams — see
/// <see cref="MatchIndexEntry"/>, which keeps only the three fields it can be trusted for. That is
/// enough here: the fixtures this leads to are real matches with real team names, and
/// <see cref="ScopedSeriesFixtures"/> applies the stricter rule to those.
/// </para>
/// <para>
/// <b>This one also saves provider calls, which none of the others do.</b> <c>PendingMatches</c>
/// resolves at most six series per pass and pays a call for each, in the order the index mentions
/// them. Unfiltered, a Sheffield Shield round and an associate tri-series could take that budget
/// and leave a Test tour unfetched. Filtering first means the six are six we cover.
/// </para>
/// </remarks>
internal sealed class ScopedMatchIndex(
    IMatchIndex inner,
    ILogger<ScopedMatchIndex> logger) : IMatchIndex
{
    public async Task<IReadOnlyList<MatchIndexEntry>> GetAsync(CancellationToken cancellationToken)
    {
        var entries = await inner.GetAsync(cancellationToken);

        var covered = entries
            .Where(entry => CricketScope.IncludesSeries(entry.SeriesName))
            .ToArray();

        if (covered.Length != entries.Count)
        {
            logger.LogDebug(
                "Scope kept {Kept} of {Total} indexed match(es); dropped {Dropped}",
                covered.Length,
                entries.Count,
                Dropped(entries.Except(covered).Select(entry => entry.SeriesName)));
        }

        return covered;
    }

    internal static string Dropped(IEnumerable<string> seriesNames)
        => string.Join(", ", seriesNames.Distinct(StringComparer.OrdinalIgnoreCase).Order());
}

/// <summary>
/// Narrows the provider's series list to the series this site covers.
/// </summary>
/// <remarks>
/// The index is the only reason an unplayed series appears at all, so this is what removes a tour
/// nobody here wants to read about before it has started. It cannot remove a series we hold
/// matches of — <c>SeriesService</c> lists those from the matches themselves — so the effect is
/// bounded to the speculative half of the list.
/// </remarks>
internal sealed class ScopedSeriesIndex(
    ISeriesIndex inner,
    ILogger<ScopedSeriesIndex> logger) : ISeriesIndex
{
    public async Task<IReadOnlyList<SeriesIndexEntry>> GetAsync(CancellationToken cancellationToken)
    {
        var entries = await inner.GetAsync(cancellationToken);

        var covered = entries
            .Where(entry => CricketScope.IncludesSeries(entry.Name))
            .ToArray();

        if (covered.Length != entries.Count)
        {
            logger.LogDebug(
                "Scope kept {Kept} of {Total} listed series",
                covered.Length,
                entries.Count);
        }

        return covered;
    }
}

/// <summary>
/// Narrows a series' fixture list to the matches this site covers.
/// </summary>
/// <remarks>
/// Applies the match rule rather than the series one, because these carry team names. That matters
/// inside a covered series: a Test tour's fixture list includes warm-up matches against invitation
/// XIs, and "Professional County Club Select XI" is not a nation.
/// </remarks>
internal sealed class ScopedSeriesFixtures(
    ISeriesFixtures inner,
    ILogger<ScopedSeriesFixtures> logger) : ISeriesFixtures
{
    public async Task<IReadOnlyList<MatchDetailsDto>> GetAsync(
        string seriesId,
        CancellationToken cancellationToken)
    {
        var fixtures = await inner.GetAsync(seriesId, cancellationToken);

        var covered = fixtures
            .Where(match => CricketScope.IncludesMatch(
                match.SeriesName,
                match.Home.Team.Name,
                match.Away.Team.Name))
            .ToArray();

        if (covered.Length != fixtures.Count)
        {
            logger.LogDebug(
                "Scope kept {Kept} of {Total} fixture(s) for series {SeriesId}",
                covered.Length,
                fixtures.Count,
                seriesId);
        }

        return covered;
    }
}
