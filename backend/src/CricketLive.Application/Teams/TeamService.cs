using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Teams.Dtos;

namespace CricketLive.Application.Teams;

/// <summary>
/// Teams assembled from the matches we already hold, in both places they live.
/// </summary>
/// <remarks>
/// <para>
/// The same design as <c>SeriesService</c>, and for a stronger reason. CricketData has no team
/// endpoint worth calling and no team identifier at all: a match names its two sides and that is
/// the whole of it. So a team is not a record we fetch and decorate — it is what the matches say,
/// and nothing else could be true of it.
/// </para>
/// <para>
/// One consequence is worth stating plainly, because it limits the page: there is no won-lost
/// record here. The provider reports a result as a sentence, and a record parsed out of prose
/// would be a guess presented as a statistic. Formats and opponents come from mapped fields and
/// are therefore real; the record does not exist and is not implied.
/// </para>
/// </remarks>
public sealed class TeamService(ICricketDataProvider provider, IMatchArchive archive) : ITeamService
{
    public async Task<IReadOnlyList<TeamSummaryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var window = await provider.GetCurrentMatchesAsync(cancellationToken);

        var archived = await archive.GetTeamTalliesAsync(
            [.. window.Select(match => match.Id)],
            cancellationToken);

        var merged = new Dictionary<string, TeamTally>(StringComparer.OrdinalIgnoreCase);

        foreach (var tally in FromWindow(window).Concat(archived))
        {
            if (string.IsNullOrWhiteSpace(tally.TeamId))
            {
                continue;
            }

            merged[tally.TeamId] = merged.TryGetValue(tally.TeamId, out var existing)
                ? existing.Merge(tally)
                : tally;
        }

        // The window carries crests and abbreviations; the archive's tally, being pure SQL, does
        // not. Looking them up from the window here means the list shows them where we have them
        // without the tally query having to read a payload to find out.
        var badges = Badges(window);

        return
        [
            .. merged.Values
                .Select(tally => ToSummary(tally, badges))
                .OrderByDescending(team => team.IsActive)
                .ThenByDescending(team => team.LastMatchUtc)
        ];
    }

    public async Task<TeamDetailsDto?> GetByIdAsync(string idOrSlug, CancellationToken cancellationToken)
    {
        var teamId = Slug.Kebab(idOrSlug);

        if (teamId.Length == 0)
        {
            return null;
        }

        var window = await provider.GetCurrentMatchesAsync(cancellationToken);
        var archived = await archive.GetByTeamAsync(teamId, cancellationToken);

        // The window's copy of a match is the fresher one, so it decides where both hold it.
        var held = window
            .Where(match => Plays(match, teamId))
            .ToDictionary(match => match.Id, StringComparer.OrdinalIgnoreCase);

        var matches = held.Values
            .Cast<MatchDto>()
            .Concat(archived.Where(match => !held.ContainsKey(match.Id)))
            .OrderBy(match => match.StartTimeUtc)
            .ToArray();

        // A team we hold no match of is indistinguishable from one that never played, so both are
        // absent rather than an empty team page that claims the side exists.
        if (matches.Length == 0)
        {
            return null;
        }

        var sides = matches.Select(match => Side(match, teamId)).ToArray();

        return new TeamDetailsDto
        {
            Team = new TeamSummaryDto
            {
                Id = teamId,
                // Longest wins, as everywhere else: the short spelling is the truncated one.
                Name = sides.MaxBy(team => team.Name.Length)?.Name ?? teamId,
                ShortName = sides.Select(team => team.ShortName).FirstOrDefault(name => name.Length > 0)
                    ?? teamId,
                LogoUrl = sides.Select(team => team.LogoUrl).FirstOrDefault(url => url is not null),
                MatchCount = matches.Length,
                FirstMatchUtc = matches[0].StartTimeUtc,
                LastMatchUtc = matches[^1].StartTimeUtc,
                IsActive = matches.Any(match => match.Status != MatchStatus.Completed),
            },
            Matches = matches,
            Series = Series(matches),
            Opponents = Opponents(matches, teamId),
            Formats = Formats(matches),
        };
    }

    private static IEnumerable<TeamTally> FromWindow(IReadOnlyList<MatchDetailsDto> window)
        => window
            .SelectMany(match => new[] { (match, match.Home.Team), (match, match.Away.Team) })
            .Where(pair => pair.Team.Id.Length > 0)
            .GroupBy(pair => pair.Team.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TeamTally
            {
                TeamId = group.Key,
                TeamName = group.MaxBy(pair => pair.Team.Name.Length).Team.Name,
                MatchCount = group.Count(),
                FirstMatchUtc = group.Min(pair => pair.match.StartTimeUtc),
                LastMatchUtc = group.Max(pair => pair.match.StartTimeUtc),
                HasUnfinished = group.Any(pair => pair.match.Status != MatchStatus.Completed),
            });

    /// <summary>The short name and crest per team id, from whichever window match carried them.</summary>
    private static Dictionary<string, TeamDto> Badges(IReadOnlyList<MatchDetailsDto> window)
    {
        var badges = new Dictionary<string, TeamDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var team in window.SelectMany(match => new[] { match.Home.Team, match.Away.Team }))
        {
            if (team.Id.Length == 0)
            {
                continue;
            }

            // First one wins unless a later one actually adds a crest, so a side seen twice does
            // not lose its logo to a copy that came without one.
            if (!badges.TryGetValue(team.Id, out var existing) || (existing.LogoUrl is null && team.LogoUrl is not null))
            {
                badges[team.Id] = team;
            }
        }

        return badges;
    }

    private static TeamSummaryDto ToSummary(TeamTally tally, Dictionary<string, TeamDto> badges)
    {
        badges.TryGetValue(tally.TeamId, out var badge);

        return new TeamSummaryDto
        {
            Id = tally.TeamId,
            Name = tally.TeamName.Length > 0 ? tally.TeamName : tally.TeamId,
            ShortName = badge?.ShortName ?? tally.TeamName,
            LogoUrl = badge?.LogoUrl,
            MatchCount = tally.MatchCount,
            FirstMatchUtc = tally.FirstMatchUtc,
            LastMatchUtc = tally.LastMatchUtc,
            IsActive = tally.HasUnfinished,
        };
    }

    private static IReadOnlyList<TeamSeriesDto> Series(IReadOnlyList<MatchDto> matches)
        =>
        [
            .. matches
                .Where(match => !string.IsNullOrWhiteSpace(match.SeriesId))
                .GroupBy(match => match.SeriesId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TeamSeriesDto
                {
                    Id = group.Key,
                    Slug = Slug.Make(group.Max(match => match.SeriesName), group.Key),
                    Name = group.Max(match => match.SeriesName) ?? string.Empty,
                    MatchCount = group.Count(),
                })
                .OrderByDescending(series => series.MatchCount)
                .ThenBy(series => series.Name, StringComparer.OrdinalIgnoreCase)
        ];

    private static IReadOnlyList<OpponentDto> Opponents(IReadOnlyList<MatchDto> matches, string teamId)
        =>
        [
            .. matches
                .Select(match => Other(match, teamId))
                .Where(team => team.Id.Length > 0)
                .GroupBy(team => team.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new OpponentDto
                {
                    Id = group.Key,
                    Name = group.MaxBy(team => team.Name.Length)!.Name,
                    MatchCount = group.Count(),
                })
                .OrderByDescending(opponent => opponent.MatchCount)
                .ThenBy(opponent => opponent.Name, StringComparer.OrdinalIgnoreCase)
        ];

    private static IReadOnlyList<FormatCountDto> Formats(IReadOnlyList<MatchDto> matches)
        =>
        [
            .. matches
                .GroupBy(match => match.Format)
                .Select(group => new FormatCountDto { Format = group.Key, MatchCount = group.Count() })
                .OrderByDescending(format => format.MatchCount)
        ];

    private static bool Plays(MatchDto match, string teamId)
        => Same(match.Home.Team.Id, teamId) || Same(match.Away.Team.Id, teamId);

    private static TeamDto Side(MatchDto match, string teamId)
        => Same(match.Home.Team.Id, teamId) ? match.Home.Team : match.Away.Team;

    private static TeamDto Other(MatchDto match, string teamId)
        => Same(match.Home.Team.Id, teamId) ? match.Away.Team : match.Home.Team;

    private static bool Same(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
