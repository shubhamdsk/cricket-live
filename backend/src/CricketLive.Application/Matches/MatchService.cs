using CricketLive.Application.Common;
using CricketLive.Application.Enrichment;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Scorecards;
using CricketLive.Application.Scorecards.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// Turns the provider's single current-matches window into the three lists the UI asks for.
/// Splitting here rather than upstream is what keeps our request cost at one call per refresh.
/// </summary>
public sealed class MatchService(
    ICricketDataProvider provider,
    IMatchEnrichmentProvider enrichment,
    IMatchScorecardProvider scorecards,
    IMatchArchive archive,
    IPendingMatches pending) : IMatchService
{
    public Task<IReadOnlyList<MatchDto>> GetLiveAsync(
        MatchFilter filter,
        CancellationToken cancellationToken)
        => FromWindowAsync(MatchStatus.Live, filter, cancellationToken);

    public Task<IReadOnlyList<MatchDto>> GetUpcomingAsync(
        MatchFilter filter,
        CancellationToken cancellationToken)
        => FromWindowAsync(MatchStatus.Upcoming, filter, cancellationToken);

    /// <summary>
    /// One status, out of the provider's window and out of everything else it will tell us is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Filtered in memory rather than upstream because both sources are single responses holding a
    /// handful of matches and the provider offers no query parameters worth the call. The archive
    /// filters in SQL for the opposite reason — it is the list that grows.
    /// </para>
    /// <para>
    /// <b>Two sources, because the main window turned out not to be the whole window.</b> It held
    /// two matches, both finished, on a day the provider's own scoreboard listed four fixtures
    /// still to be played, so the upcoming page was empty for want of asking rather than for want
    /// of cricket. The window still wins on any match in both: it is the only one of the two that
    /// carries a score, so a match that has started reads correctly from it and reads as unplayed
    /// from the other.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<MatchDto>> FromWindowAsync(
        MatchStatus status,
        MatchFilter filter,
        CancellationToken cancellationToken)
    {
        // A caller asking for live matches while filtering to completed ones is contradicting
        // itself. Empty is the honest answer, and it costs no provider call to say so.
        if (filter.Status is { } wanted && wanted != status)
        {
            return [];
        }

        var window = await provider.GetCurrentMatchesAsync(cancellationToken);
        var imminent = await pending.GetAsync(cancellationToken);

        var byId = new Dictionary<string, MatchDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in imminent)
        {
            byId[match.Id] = match;
        }

        foreach (var match in window)
        {
            byId[match.Id] = match;
        }

        return [.. byId.Values
            .Where(match => match.Status == status && filter.Matches(match))
            .OrderBy(match => match.StartTimeUtc)];
    }

    /// <summary>
    /// Finished matches from the provider's window and from everything we kept before it moved on.
    /// </summary>
    /// <remarks>
    /// The window is only a few days wide, so on its own it answers "what happened today" and
    /// nothing more. Merging is needed rather than reading the archive alone because a match that
    /// finished minutes ago is in the window before it is anywhere else.
    /// </remarks>
    public async Task<(IReadOnlyList<MatchDto> Matches, int Total)> GetResultsAsync(
        MatchFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (filter.Status is { } wanted && wanted != MatchStatus.Completed)
        {
            return ([], 0);
        }

        // Fetching the window is also what archives it, so by the time the archive is read below it
        // already holds everything the window could contribute. The archive is the source here, and
        // because it is, a provider outage costs this list the last few minutes rather than all of
        // it — see ProviderWindow for the day that distinction was learned the hard way.
        var window = await ProviderWindow.OrEmptyAsync(provider, cancellationToken);

        var stored = await archive.GetFinishedAsync(filter, page.Skip, page.Take, cancellationToken);
        var total = await archive.CountFinishedAsync(filter, cancellationToken);

        if (page.Skip > 0)
        {
            return (stored, total);
        }

        // Safety net, and only worth applying to the first page: if a write failed, a match that
        // finished minutes ago would disappear from results altogether rather than merely being
        // absent from history.
        var held = stored.Select(match => match.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rescued = window
            .Where(match => match.Status == MatchStatus.Completed
                && !held.Contains(match.Id)
                && filter.Matches(match))
            .ToArray();

        if (rescued.Length == 0)
        {
            return (stored, total);
        }

        var merged = rescued
            .Concat<MatchDto>(stored)
            .OrderByDescending(match => match.StartTimeUtc)
            .Take(page.Take)
            .ToArray();

        return (merged, total + rescued.Length);
    }

    /// <summary>
    /// Every series we can actually show a match for, from both places matches live.
    /// </summary>
    /// <remarks>
    /// All three sources, because each alone would offer a filter that finds nothing. The archive
    /// knows nothing about a tournament that started this morning, the window has forgotten one
    /// that finished last week, and neither mentions a tour whose first match is on Tuesday. None
    /// is a superset of another, and the page this feeds shows matches from all three.
    /// </remarks>
    public async Task<IReadOnlyList<string>> GetSeriesNamesAsync(CancellationToken cancellationToken)
    {
        var window = await ProviderWindow.OrEmptyAsync(provider, cancellationToken);
        var imminent = await pending.GetAsync(cancellationToken);
        // Nothing is excluded because nothing here is counted: this reduces to a distinct set of
        // names, so a series appearing in two sources costs a duplicate that Distinct removes.
        var archived = await archive.GetSeriesTalliesAsync([], cancellationToken);

        return
        [
            .. window
                .Select(match => match.SeriesName)
                .Concat(imminent.Select(match => match.SeriesName))
                .Concat(archived.Select(tally => tally.SeriesName))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        ];
    }

    public async Task<MatchDetailsDto?> GetByIdAsync(string matchId, CancellationToken cancellationToken)
    {
        var match = await provider.GetMatchAsync(matchId, cancellationToken);

        // Only a match in progress has anyone at the crease, and only the detail view shows them,
        // so this is the one place and the one moment enrichment is worth a second request.
        if (match is null || match.Status != MatchStatus.Live)
        {
            return match;
        }

        return await Enrich.WithBattersAsync(match, enrichment, cancellationToken);
    }

    public async Task<ScorecardDto?> GetScorecardAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        // The match is fetched first even though the scorecard comes from elsewhere, because the
        // scorecard source needs to be told the teams and the series to find its own copy of the
        // match. This costs nothing: it is the same cached call the match page already made.
        var match = await provider.GetMatchAsync(matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var identity = new MatchIdentity(
            match.Id,
            match.MatchTitle,
            match.SeriesName,
            match.Home.Team.ShortName,
            match.Away.Team.ShortName);

        return await scorecards.GetScorecardAsync(identity, cancellationToken);
    }
}
