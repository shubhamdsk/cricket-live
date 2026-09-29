namespace CricketLive.Application.Matches.Dtos;

/// <param name="Slug">Readable identifier ending in <paramref name="Id"/>, so a pretty URL stays resolvable.</param>
/// <param name="StatusText">The provider's own sentence, such as "India won by 8 wkts". Shown verbatim, never paraphrased.</param>
public record MatchDto
{
    public required string Id { get; init; }

    public required string Slug { get; init; }

    public required MatchStatus Status { get; init; }

    public required MatchFormat Format { get; init; }

    public required string SeriesName { get; init; }

    public required string MatchTitle { get; init; }

    public required string Venue { get; init; }

    public required DateTimeOffset StartTimeUtc { get; init; }

    public required TeamInningsDto Home { get; init; }

    public required TeamInningsDto Away { get; init; }

    public required string StatusText { get; init; }
}
