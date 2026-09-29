using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Teams.Dtos;

/// <summary>
/// One team, as a list of teams shows it.
/// </summary>
/// <remarks>
/// Named "summary" rather than "team" because <see cref="TeamDto"/> already exists and means
/// something narrower: the side of a single match, carried inside a scorecard. This is the team
/// across every match we hold.
/// </remarks>
public sealed record TeamSummaryDto
{
    /// <summary>The slug this team is addressed by, and its only identifier.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The abbreviation, such as IND, when a match gave us one.</summary>
    /// <remarks>
    /// Falls back to the name. Not every side has a short form, and inventing three letters from
    /// a name would produce abbreviations nobody uses.
    /// </remarks>
    public required string ShortName { get; init; }

    /// <summary>The team's crest, when the provider supplied one.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>
    /// How many matches we hold for this team — not how many it has played.
    /// </summary>
    /// <remarks>
    /// The distinction matters and the UI states it. The archive starts the day it shipped and the
    /// provider's window is days wide, so this is a count of our records, not of history.
    /// </remarks>
    public required int MatchCount { get; init; }

    public required DateTimeOffset FirstMatchUtc { get; init; }

    public required DateTimeOffset LastMatchUtc { get; init; }

    /// <summary>Whether this team is in a match that has not finished.</summary>
    public required bool IsActive { get; init; }
}
