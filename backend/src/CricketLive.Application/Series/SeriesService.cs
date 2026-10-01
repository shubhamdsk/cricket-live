using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series.Dtos;

namespace CricketLive.Application.Series;

/// <summary>
/// Series assembled from the matches we hold, listed from the provider's index.
/// </summary>
/// <remarks>
/// <para>
/// Two sources doing two jobs. The matches — in the provider's window and in our archive — are
/// what a series page can actually show. The index is only consulted for which series exist, and
/// it exists in this class because without it the list was a function of the window: in a quiet
/// week the provider's window held two matches of one tour, so this returned exactly one series
/// and read as a fault rather than as the narrow window it was.
/// </para>
/// <para>
/// The original note here said the index "would learn less than the matches already in memory can
/// say", and that was right about the fields and wrong about the question. It answers a different
/// question — existence, not detail — and the two do not compete. It is read for an id, a name, a
/// start date and a match count, and ignored for everything else.
/// </para>
/// <para>
/// The one provider field this depends on either way is <c>series_id</c>, which arrives free on
/// every match and is what makes a series identifiable at all: the name reaches us as the tail of
/// a free-text field, and two matches of one series do not always spell it the same way.
/// </para>
/// </remarks>
public sealed class SeriesService(
    ICricketDataProvider provider,
    IMatchArchive archive,
    ISeriesStandingsProvider standings,
    ISeriesIndex index,
    ISeriesFixtures fixtures) : ISeriesService
{
    public async Task<IReadOnlyList<SeriesDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var held = await GatherAsync(cancellationToken);
        var listed = await ListedAsync(held, cancellationToken);

        // Read once so every comparison below uses the same instant. Sorting against a clock that
        // moves mid-sort is the kind of thing that produces an ordering no comparer could justify.
        var now = DateTimeOffset.UtcNow;

        return
        [
            .. listed
                .OrderByDescending(series => series.IsOngoing)
                .ThenByDescending(series => series.MatchCount > 0)
                .ThenBy(series => Distance(series, now))
        ];
    }

    /// <summary>
    /// How far a series sits from today, in either direction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ordering this feeds is three questions in order: is a match being played right now, can
    /// we show anything of this series, and how close to today is it. Only the first two are facts
    /// about the data; this one is the tie-break, and it is unsigned on purpose.
    /// </para>
    /// <para>
    /// Plain descending-by-date was tried first and read badly. The index lists fixtures a year
    /// ahead, so the newest series was a tour in March 2027 and the page opened on something
    /// nobody can watch, with this week's cricket far below it. Sorting by distance instead puts
    /// the current week at the top and lets the page fall away in both directions, which needs no
    /// end dates — and we have none, because the provider sends <c>endDate</c> without a year.
    /// </para>
    /// <para>
    /// A series we hold matches of is measured from its last match, because that is the most
    /// recent thing we know happened in it. One we hold nothing of is measured from its start,
    /// which is the only date it has.
    /// </para>
    /// </remarks>
    private static TimeSpan Distance(SeriesDto series, DateTimeOffset now)
    {
        var known = series.LastMatchUtc ?? series.StartTimeUtc;

        return known > now ? known - now : now - known;
    }

    /// <summary>
    /// Every series worth listing: the ones we hold matches of, plus the ones the index knows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Held matches win every field they can speak to. The index contributes a match total to a
    /// series we already had, and a whole entry for one we did not — never a date, because a
    /// series page shows held matches and a span wider than those matches would describe a page
    /// that does not exist.
    /// </para>
    /// <para>
    /// Note which way the merge runs. The index cannot remove a series: anything derived from a
    /// real match stays listed whether the index covers it or not, so a narrow page budget makes
    /// the list shorter without ever making it wrong.
    /// </para>
    /// </remarks>
    private async Task<IEnumerable<SeriesDto>> ListedAsync(
        Dictionary<string, SeriesTally> held,
        CancellationToken cancellationToken)
    {
        var merged = held.Values.ToDictionary(
            tally => tally.SeriesId,
            ToDto,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in await index.GetAsync(cancellationToken))
        {
            merged[entry.Id] = merged.TryGetValue(entry.Id, out var ours)
                ? Enrich(ours, entry)
                : FromIndex(entry);
        }

        return merged.Values;
    }

    /// <summary>
    /// What the index can add to a series we already described from its matches.
    /// </summary>
    /// <remarks>
    /// A total and possibly a fuller name, and nothing else. Not the dates: a series page shows
    /// held matches, so a span reaching past them would describe a page that does not exist.
    /// </remarks>
    private static SeriesDto Enrich(SeriesDto ours, SeriesIndexEntry entry) => ours with
    {
        // The longer spelling wins, on the same reasoning as SeriesTally.Merge: the disagreements
        // observed are truncations rather than genuinely different names.
        Name = entry.Name.Length > ours.Name.Length ? entry.Name : ours.Name,
        TotalMatchCount = entry.MatchCount,
    };

    /// <summary>A series the index lists and we hold no match of.</summary>
    private static SeriesDto FromIndex(SeriesIndexEntry entry) => new()
    {
        Id = entry.Id,
        Slug = Slug.Make(entry.Name, entry.Id),
        Name = entry.Name,
        StartTimeUtc = entry.StartTimeUtc,
        LastMatchUtc = null,
        MatchCount = 0,
        TotalMatchCount = entry.MatchCount,
        IsOngoing = false,
    };

    /// <summary>
    /// The index's entry for one series, or <see langword="null"/> when it does not cover it.
    /// </summary>
    /// <remarks>
    /// Cheap to call more than once: the implementation caches for hours, so this reads the same
    /// list the series page just read rather than paying for it again.
    /// </remarks>
    private async Task<SeriesIndexEntry?> ListedAsync(string seriesId, CancellationToken cancellationToken)
        => (await index.GetAsync(cancellationToken)).FirstOrDefault(listed => Same(listed.Id, seriesId));

    public async Task<SeriesDetailsDto?> GetByIdAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        if (!Slug.TryExtractId(idOrSlug, out var seriesId))
        {
            return null;
        }

        var tallies = await GatherAsync(cancellationToken);
        tallies.TryGetValue(seriesId, out var tally);

        // The same enrichment the list does, so the two cannot disagree about one series. Without
        // it a card read "2 of 8 matches held" and the page it opened read "2 matches held",
        // which is the sort of difference a reader notices and cannot explain.
        var listed = await ListedAsync(seriesId, cancellationToken);

        // Neither source has heard of it, so there is no name to put at the top of a page. That is
        // a narrower 404 than it used to be: a series we hold nothing of but the index lists now
        // renders, because listing something and then refusing to open it is worse than not
        // listing it at all.
        if (tally is null && listed is null)
        {
            return null;
        }

        var matches = await MatchesOfAsync(seriesId, cancellationToken);

        var series = (tally, listed) switch
        {
            (not null, not null) => Enrich(ToDto(tally), listed),
            (not null, null) => ToDto(tally),
            _ => FromIndex(listed!),
        };

        return new SeriesDetailsDto
        {
            Series = series,
            Matches = matches,
            // Asked for only when there is something to ask about. The standings source finds its
            // copy of a series by name and by the teams in its matches, so with no matches there
            // is nothing to match on and the request would spend itself being told no.
            Standings = matches.Count == 0
                ? []
                : await standings.GetAsync(series, cancellationToken),
        };
    }

    /// <summary>
    /// Every match of a series we can show, from the three places they come from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Precedence runs weakest to strongest, each source overwriting the last by match id. The
    /// provider's fixture list is weakest: it is the only one that knows a match exists before it
    /// is played, and the only one that may carry no score for one that has been. The archive is
    /// our own captured copy. The window is strongest, because a match that finished minutes ago is
    /// right there before it is anywhere else.
    /// </para>
    /// <para>
    /// This is what turned a series page from two matches into the whole tour. Before it, the page
    /// could only show matches that had passed through our window since the site started — so a
    /// tour's remaining fixtures, which the provider knows about and a reader wants most, were the
    /// one thing it could not display.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<MatchDto>> MatchesOfAsync(
        string seriesId,
        CancellationToken cancellationToken)
    {
        // Every one of the three is optional here, and between them a series page survives any
        // single source being unavailable: the schedule, our own records, and what is in play.
        var window = await ProviderWindow.OrEmptyAsync(provider, cancellationToken);
        var archived = await archive.GetBySeriesAsync(seriesId, cancellationToken);
        var scheduled = await fixtures.GetAsync(seriesId, cancellationToken);

        var byId = new Dictionary<string, MatchDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in scheduled)
        {
            byId[match.Id] = match;
        }

        foreach (var match in archived)
        {
            byId[match.Id] = match;
        }

        foreach (var match in window.Where(match => Same(match.SeriesId, seriesId)))
        {
            byId[match.Id] = match;
        }

        return [.. byId.Values.OrderBy(match => match.StartTimeUtc)];
    }

    /// <summary>
    /// One tally per series, from both sources, keyed by the provider's series id.
    /// </summary>
    /// <remarks>
    /// Matches with no series id are dropped rather than grouped under an empty key. A series page
    /// needs an id to be addressable, so a match without one has no page to belong to, and
    /// collecting them together would invent a series that does not exist.
    /// </remarks>
    private async Task<Dictionary<string, SeriesTally>> GatherAsync(CancellationToken cancellationToken)
    {
        var window = await ProviderWindow.OrEmptyAsync(provider, cancellationToken);

        // The window's matches are counted here, so the archive must not count them again: one
        // that finished minutes ago is in both, and a series would claim a match more than it has.
        var archived = await archive.GetSeriesTalliesAsync(
            [.. window.Select(match => match.Id)],
            cancellationToken);

        var fromWindow = window
            .Where(match => !string.IsNullOrWhiteSpace(match.SeriesId))
            .GroupBy(match => match.SeriesId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SeriesTally
            {
                SeriesId = group.Key,
                SeriesName = group.Max(match => match.SeriesName) ?? string.Empty,
                MatchCount = group.Count(),
                FirstMatchUtc = group.Min(match => match.StartTimeUtc),
                LastMatchUtc = group.Max(match => match.StartTimeUtc),
                HasUnfinished = group.Any(match => match.Status != MatchStatus.Completed),
            });

        var merged = new Dictionary<string, SeriesTally>(StringComparer.OrdinalIgnoreCase);

        foreach (var tally in fromWindow.Concat(archived.Where(t => !string.IsNullOrWhiteSpace(t.SeriesId))))
        {
            merged[tally.SeriesId] = merged.TryGetValue(tally.SeriesId, out var existing)
                ? existing.Merge(tally)
                : tally;
        }

        return merged;
    }

    private static SeriesDto ToDto(SeriesTally tally) => new()
    {
        Id = tally.SeriesId,
        Slug = Slug.Make(tally.SeriesName, tally.SeriesId),
        Name = tally.SeriesName,
        StartTimeUtc = tally.FirstMatchUtc,
        LastMatchUtc = tally.LastMatchUtc,
        MatchCount = tally.MatchCount,
        // Left unset on purpose. A tally counts matches; only the index knows the series total,
        // and it is filled in afterwards for the series it covers.
        TotalMatchCount = null,
        IsOngoing = tally.HasUnfinished,
    };

    private static bool Same(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
