using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Search.Dtos;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;

namespace CricketLive.Application.Search;

/// <summary>Searches what we hold: matches, the teams in them, and the series they belong to.</summary>
public interface ISearchService
{
    Task<SearchResultsDto> SearchAsync(string query, CancellationToken cancellationToken);
}

/// <summary>
/// One query answered across three kinds of thing, from data already in hand.
/// </summary>
/// <remarks>
/// <para>
/// Substring matching in memory, not a text index, and that is a sizing decision rather than a
/// shortcut. The searchable set is the provider's window plus the archive — hundreds of rows, not
/// millions — and the window has to be fetched anyway. A SQLite FTS table would add a schema, a
/// sync path and a second definition of what counts as a match, to speed up a scan that takes
/// less time than the provider call it depends on. If the archive grows to the point this matters,
/// the seam to replace is this class.
/// </para>
/// <para>
/// Ordering within a group puts a title that starts with the query above one that merely contains
/// it, which is the whole of the ranking. Anything cleverer would be a relevance score invented
/// without evidence that it helps.
/// </para>
/// </remarks>
public sealed class SearchService(
    ICricketDataProvider provider,
    IMatchArchive archive,
    ISeriesService series,
    ITeamService teams) : ISearchService
{
    /// <summary>
    /// The shortest query worth answering.
    /// </summary>
    /// <remarks>
    /// One character matches most of everything, which is not a search result but a list with a
    /// delay in front of it. The UI debounces; this is the other half of the same guard.
    /// </remarks>
    private const int MinimumQueryLength = 2;

    /// <summary>
    /// The longest query worth answering, and the point past which one is refused rather than run.
    /// </summary>
    /// <remarks>
    /// Kestrel caps a request line at 8 KB, so without this the term reaching us can be thousands
    /// of characters — searched against every field of every match we hold, and then echoed back
    /// in <see cref="SearchResultsDto.Query"/>. Nothing we store has a name this long, so a longer
    /// query cannot match and is truncated to the longest one that could.
    /// </remarks>
    private const int MaximumQueryLength = 128;

    /// <summary>How many hits per group. Enough to choose from, few enough to read.</summary>
    private const int PerGroup = 10;

    public async Task<SearchResultsDto> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length < MinimumQueryLength)
        {
            return Empty(trimmed);
        }

        // Truncated rather than rejected, for the same reason a short query is not a 400: the
        // caller is typing, not making a mistake. A term this long has no hits either way, so the
        // only thing at stake is how much work we do to find that out.
        if (trimmed.Length > MaximumQueryLength)
        {
            trimmed = trimmed[..MaximumQueryLength];
        }

        // Optional: search over the archive alone is a narrower search, which is a far better
        // answer than refusing to search at all because the provider is down.
        var window = await ProviderWindow.OrEmptyAsync(provider, cancellationToken);

        // The archive is read unfiltered and matched in memory. Pushing the term into SQL would
        // mean LIKE against a payload column, which is neither indexed nor safe for a term
        // containing a wildcard.
        var archived = await archive.GetFinishedAsync(MatchFilter.None, 0, 200, cancellationToken);

        var held = window
            .Cast<MatchDto>()
            .Concat(archived)
            .GroupBy(match => match.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        var matchHits = held
            .Where(match => Hits(trimmed, match.MatchTitle, match.SeriesName, match.Venue,
                match.Home.Team.Name, match.Away.Team.Name))
            .Select(match => new SearchHitDto
            {
                Id = match.Slug,
                Title = match.MatchTitle.Length > 0 ? match.MatchTitle : Versus(match),
                Subtitle = match.SeriesName,
            });

        var teamHits = (await teams.GetAllAsync(cancellationToken))
            .Where(team => Hits(trimmed, team.Name, team.ShortName))
            .Select(team => new SearchHitDto
            {
                Id = team.Id,
                Title = team.Name,
                Subtitle = Held(team.MatchCount),
            });

        var seriesHits = (await series.GetAllAsync(cancellationToken))
            .Where(item => Hits(trimmed, item.Name))
            .Select(item => new SearchHitDto
            {
                Id = item.Slug,
                Title = item.Name,
                Subtitle = Held(item),
            });

        var matchResults = Rank(trimmed, matchHits);
        var teamResults = Rank(trimmed, teamHits);
        var seriesResults = Rank(trimmed, seriesHits);

        return new SearchResultsDto
        {
            Query = trimmed,
            Matches = matchResults,
            Teams = teamResults,
            Series = seriesResults,
            Total = matchResults.Count + teamResults.Count + seriesResults.Count,
        };
    }

    private static SearchResultsDto Empty(string query) => new()
    {
        Query = query,
        Matches = [],
        Teams = [],
        Series = [],
        Total = 0,
    };

    /// <summary>True when the query appears in any of the fields worth searching.</summary>
    private static bool Hits(string query, params string?[] fields)
        => fields.Any(field =>
            field is not null
            && field.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<SearchHitDto> Rank(string query, IEnumerable<SearchHitDto> hits)
        =>
        [
            .. hits
                .OrderByDescending(hit => hit.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenBy(hit => hit.Title, StringComparer.OrdinalIgnoreCase)
                .Take(PerGroup)
        ];

    /// <summary>
    /// "3 matches held", never "3 matches played".
    /// </summary>
    /// <remarks>
    /// The archive starts the day it shipped, so a count is of our records rather than of history,
    /// and the wording says so everywhere it appears.
    /// </remarks>
    private static string Held(int count)
        => count == 1 ? "1 match held" : $"{count} matches held";

    /// <summary>
    /// The same three readings the series pages use, and for the same reason.
    /// </summary>
    /// <remarks>
    /// A series the provider's index lists and we hold no match of would otherwise come back as
    /// "0 matches held" — true, and it reads as a fault. Saying how many the series has instead
    /// turns the zero into a fact about our records rather than about the cricket.
    /// </remarks>
    private static string Held(Series.Dtos.SeriesDto series) => series switch
    {
        { MatchCount: 0, TotalMatchCount: null } => "No matches held yet",
        // The total agrees with the noun, not the held count: "1 of 1 match held". A one-match
        // series does exist — the provider lists tours with a single fixture.
        { MatchCount: 0, TotalMatchCount: { } total } => $"None of {total} {Matches(total)} held yet",
        { TotalMatchCount: null } => Held(series.MatchCount),
        { MatchCount: var held, TotalMatchCount: { } total } => $"{held} of {total} {Matches(total)} held",
    };

    private static string Matches(int count) => count == 1 ? "match" : "matches";

    private static string Versus(MatchDto match)
        => $"{match.Home.Team.Name} vs {match.Away.Team.Name}";
}
