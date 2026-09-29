using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series.Dtos;

namespace CricketLive.Application.Series;

/// <summary>
/// Series assembled from the matches we already hold, in both places they live.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here calls the provider for series data, and that is the design rather than an
/// omission. CricketData has a <c>series</c> endpoint; it was measured before this was written
/// and returns an index, not a series: no standings at all, <c>endDate</c> never an ISO date, and
/// squads empty for every one of the twenty-five sampled. Each read of it would spend one of a
/// hundred daily calls to learn less than the matches already in memory can say.
/// </para>
/// <para>
/// The one provider field this does depend on is <c>series_id</c>, which arrives free on every
/// match. That is what makes a series identifiable at all: the name reaches us as the tail of a
/// free-text field, and two matches of one series do not always spell it the same way.
/// </para>
/// </remarks>
public sealed class SeriesService(
    ICricketDataProvider provider,
    IMatchArchive archive,
    ISeriesStandingsProvider standings) : ISeriesService
{
    public async Task<IReadOnlyList<SeriesDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var tallies = await GatherAsync(cancellationToken);

        return
        [
            .. tallies.Values
                .Select(ToDto)
                .OrderByDescending(series => series.IsOngoing)
                .ThenByDescending(series => series.LastMatchUtc)
        ];
    }

    public async Task<SeriesDetailsDto?> GetByIdAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        if (!Slug.TryExtractId(idOrSlug, out var seriesId))
        {
            return null;
        }

        var tallies = await GatherAsync(cancellationToken);

        if (!tallies.TryGetValue(seriesId, out var tally))
        {
            return null;
        }

        var window = await provider.GetCurrentMatchesAsync(cancellationToken);
        var archived = await archive.GetBySeriesAsync(seriesId, cancellationToken);

        // The window holds a match that finished minutes ago before the archive does, and the
        // archive holds one the window has forgotten. Taken together they double-count whatever
        // sits in both, so the id decides and the window's copy is the fresher one.
        var held = window
            .Where(match => Same(match.SeriesId, seriesId))
            .ToDictionary(match => match.Id, StringComparer.OrdinalIgnoreCase);

        var matches = held.Values
            .Cast<MatchDto>()
            .Concat(archived.Where(match => !held.ContainsKey(match.Id)))
            .OrderBy(match => match.StartTimeUtc)
            .ToArray();

        var series = ToDto(tally);

        return new SeriesDetailsDto
        {
            Series = series,
            Matches = matches,
            Standings = await standings.GetAsync(series, cancellationToken),
        };
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
        var window = await provider.GetCurrentMatchesAsync(cancellationToken);
        var archived = await archive.GetSeriesTalliesAsync(cancellationToken);

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
        IsOngoing = tally.HasUnfinished,
    };

    private static bool Same(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
