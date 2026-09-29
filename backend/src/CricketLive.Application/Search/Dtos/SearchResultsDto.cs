namespace CricketLive.Application.Search.Dtos;

/// <summary>
/// What one query found, grouped by what kind of thing it is.
/// </summary>
/// <remarks>
/// <para>
/// Grouped rather than one ranked list, because the groups are not comparable: a team and a match
/// are not more or less relevant than each other, and interleaving them would need a scoring rule
/// invented for the purpose. Grouping lets the UI show headed sections and lets a caller ignore
/// the kinds it does not want.
/// </para>
/// <para>
/// Players are absent, and that is not an oversight. No source available to us links a player to
/// a match, and the provider's player index holds only a name and a country — searching it would
/// return people we can say nothing further about. See docs/decisions.md.
/// </para>
/// </remarks>
public sealed record SearchResultsDto
{
    /// <summary>The query as it was interpreted, trimmed.</summary>
    public required string Query { get; init; }

    public required IReadOnlyList<SearchHitDto> Matches { get; init; }

    public required IReadOnlyList<SearchHitDto> Teams { get; init; }

    public required IReadOnlyList<SearchHitDto> Series { get; init; }

    /// <summary>Hits across all three groups, so a caller need not add them up.</summary>
    public required int Total { get; init; }
}

/// <summary>
/// One thing found, with enough to render a row and follow it.
/// </summary>
/// <remarks>
/// Deliberately one shape for all three kinds. A match, a team and a series each reduce to a
/// title, a line of context and a route, and three near-identical types would be three places to
/// change when a row gains a field.
/// </remarks>
public sealed record SearchHitDto
{
    /// <summary>The identifier or slug this kind of thing is addressed by.</summary>
    public required string Id { get; init; }

    /// <summary>What the row reads as: a match title, a team name, a series name.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// The line beneath it — the series for a match, the match count for a team.
    /// </summary>
    /// <remarks>Empty rather than null when there is nothing useful to add.</remarks>
    public required string Subtitle { get; init; }
}
