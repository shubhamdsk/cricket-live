using System.Globalization;
using CricketLive.Application.Common;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Media;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// Translates provider models into application DTOs. Everything the provider gets wrong or leaves
/// out is absorbed here, so nothing downstream has to know which provider we use.
/// </summary>
internal sealed class CricketDataMatchMapper(ILogger<CricketDataMatchMapper> logger)
{
    public MatchDetailsDto? ToMatchDetails(CricketDataMatch source)
    {
        if (string.IsNullOrWhiteSpace(source.Id))
        {
            logger.LogWarning("Discarding a provider match with no id: {Name}", source.Name);
            return null;
        }

        var (matchTitle, seriesName) = SplitName(source.Name);
        var (home, away) = BuildSides(source);

        return new MatchDetailsDto
        {
            Id = source.Id,
            Slug = BuildSlug(source.Name, source.Id),
            Status = ResolveStatus(source),
            Format = ResolveFormat(source.MatchType),
            SeriesId = source.SeriesId?.Trim() ?? string.Empty,
            SeriesName = seriesName,
            MatchTitle = matchTitle,
            Venue = source.Venue?.Trim() ?? string.Empty,
            StartTimeUtc = ParseStartTime(source.DateTimeGmt),
            Home = home,
            Away = away,
            StatusText = source.Status?.Trim() ?? string.Empty,
            HasBallByBall = source.BallByBallEnabled,
            HasSquads = source.HasSquad
        };
    }

    private static MatchStatus ResolveStatus(CricketDataMatch source) => source switch
    {
        { MatchEnded: true } => MatchStatus.Completed,
        { MatchStarted: true } => MatchStatus.Live,
        _ => MatchStatus.Upcoming
    };

    private static MatchFormat ResolveFormat(string? matchType) => matchType?.Trim().ToLowerInvariant() switch
    {
        "t20" => MatchFormat.T20,
        "odi" => MatchFormat.Odi,
        "test" => MatchFormat.Test,
        _ => MatchFormat.Other
    };

    /// <summary>
    /// The provider packs three things into one name: "India vs West Indies, 1st ODI, West Indies tour of India, 2026".
    /// The teams come first, the match description second, and whatever remains is the series.
    /// </summary>
    private static (string MatchTitle, string SeriesName) SplitName(string? name)
    {
        var text = name?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return (string.Empty, string.Empty);
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            >= 3 => (parts[1], string.Join(", ", parts[2..])),
            2 => (parts[1], string.Empty),
            _ => (text, string.Empty)
        };
    }

    /// <summary>
    /// A readable slug from the teams only — the leading segment of the provider's composite name,
    /// before the match description and series it packs in after the first comma.
    /// </summary>
    private static string BuildSlug(string? name, string id)
        => Slug.Make(name?.Split(',', 2, StringSplitOptions.TrimEntries).FirstOrDefault(), id);

    /// <summary>The provider sends "2026-09-27T08:30:00" with no offset and means UTC by it.</summary>
    private static DateTimeOffset ParseStartTime(string? value)
    {
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.MinValue;
    }

    private (TeamInningsDto Home, TeamInningsDto Away) BuildSides(CricketDataMatch source)
    {
        var names = source.Teams ?? [];
        var homeTeam = BuildTeam(names.ElementAtOrDefault(0), source.TeamInfo);
        var awayTeam = BuildTeam(names.ElementAtOrDefault(1), source.TeamInfo);

        var homeInnings = new List<InningsScoreDto>();
        var awayInnings = new List<InningsScoreDto>();

        foreach (var entry in source.Score ?? [])
        {
            var (battingTeam, number) = InningsLabel.Parse(entry.Inning);
            var innings = new InningsScoreDto(
                number,
                entry.Runs,
                entry.Wickets,
                entry.Overs.ToString("0.#", CultureInfo.InvariantCulture));

            if (Matches(battingTeam, homeTeam.Name))
            {
                homeInnings.Add(innings);
            }
            else if (Matches(battingTeam, awayTeam.Name))
            {
                awayInnings.Add(innings);
            }
            else
            {
                // Dropping beats guessing: a score shown against the wrong side is worse than one not shown.
                logger.LogWarning(
                    "Could not attribute innings {Label} to either side of {Match}; the innings was dropped",
                    entry.Inning,
                    source.Name);
            }
        }

        return (
            new TeamInningsDto(homeTeam, [.. homeInnings.OrderBy(innings => innings.Number)]),
            new TeamInningsDto(awayTeam, [.. awayInnings.OrderBy(innings => innings.Number)]));
    }

    private static bool Matches(string candidate, string teamName) =>
        !string.IsNullOrEmpty(candidate)
        && !string.IsNullOrEmpty(teamName)
        && (teamName.Equals(candidate, StringComparison.OrdinalIgnoreCase)
            || teamName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(teamName, StringComparison.OrdinalIgnoreCase));

    private static TeamDto BuildTeam(string? name, List<CricketDataTeamInfo>? teamInfo)
    {
        var teamName = name?.Trim() ?? string.Empty;

        var info = teamInfo?.FirstOrDefault(candidate =>
            string.Equals(candidate.Name?.Trim(), teamName, StringComparison.OrdinalIgnoreCase));

        return new TeamDto(
            Slug.Kebab(teamName),
            teamName,
            Abbreviate(info?.ShortName, teamName),
            // Never the provider's own address: their terms forbid a browser fetching it. See
            // CrestUrl, which also handles the blank and absent cases.
            CrestUrl.ToProxyPath(info?.Image));
    }

    /// <summary>
    /// The provider only supplies short names for teams it has a profile for, so domestic sides
    /// arrive without one. Initials read better on a narrow card than a truncated full name.
    /// </summary>
    private static string Abbreviate(string? shortName, string teamName)
    {
        if (!string.IsNullOrWhiteSpace(shortName))
        {
            return shortName.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(teamName))
        {
            return string.Empty;
        }

        var words = teamName.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (words.Length > 1)
        {
            return string.Concat(words.Take(3).Select(word => char.ToUpperInvariant(word[0])));
        }

        return teamName[..Math.Min(3, teamName.Length)].ToUpperInvariant();
    }
}
