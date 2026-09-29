namespace CricketLive.Application.Matches.Dtos;

/// <param name="Id">Slug derived from the name. The provider does not issue team identifiers.</param>
/// <param name="ShortName">Three-letter abbreviation such as IND. Falls back to the full name.</param>
public sealed record TeamDto(
    string Id,
    string Name,
    string ShortName,
    string? LogoUrl);
