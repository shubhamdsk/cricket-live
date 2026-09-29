using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Teams.Dtos;

/// <summary>
/// A team's page: who they are, what we hold, and who they played.
/// </summary>
public sealed record TeamDetailsDto
{
    public required TeamSummaryDto Team { get; init; }

    /// <summary>Every match we hold with this team on either side, earliest first.</summary>
    public required IReadOnlyList<MatchDto> Matches { get; init; }

    /// <summary>
    /// The series those matches belong to, most recent first.
    /// </summary>
    /// <remarks>
    /// Derived from the matches rather than looked up, so it cannot disagree with them, and it is
    /// what makes a team page a route onward rather than a dead end.
    /// </remarks>
    public required IReadOnlyList<TeamSeriesDto> Series { get; init; }

    /// <summary>
    /// Sides this team has faced, with how often.
    /// </summary>
    /// <remarks>
    /// Deliberately not a head-to-head record. Who won is available only as the provider's own
    /// sentence, and parsing it into wins would publish a guess as a statistic. See
    /// <see cref="TeamTally"/>.
    /// </remarks>
    public required IReadOnlyList<OpponentDto> Opponents { get; init; }

    /// <summary>
    /// How many of those matches were played in each format.
    /// </summary>
    /// <remarks>
    /// Format is a mapped enum rather than parsed prose, so unlike a win record this is a fact
    /// the data actually supports.
    /// </remarks>
    public required IReadOnlyList<FormatCountDto> Formats { get; init; }
}

/// <summary>A series this team appears in, enough to link to it.</summary>
public sealed record TeamSeriesDto
{
    public required string Id { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public required int MatchCount { get; init; }
}

/// <summary>A side faced, and how many times.</summary>
public sealed record OpponentDto
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int MatchCount { get; init; }
}

/// <summary>A format played, and how many times.</summary>
public sealed record FormatCountDto
{
    public required MatchFormat Format { get; init; }

    public required int MatchCount { get; init; }
}
