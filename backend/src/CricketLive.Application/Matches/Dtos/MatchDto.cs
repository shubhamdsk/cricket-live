namespace CricketLive.Application.Matches.Dtos;

/// <param name="Slug">Readable identifier ending in <paramref name="Id"/>, so a pretty URL stays resolvable.</param>
/// <param name="StatusText">The provider's own sentence, such as "India won by 8 wkts". Shown verbatim, never paraphrased.</param>
public record MatchDto
{
    public required string Id { get; init; }

    public required string Slug { get; init; }

    public required MatchStatus Status { get; init; }

    public required MatchFormat Format { get; init; }

    /// <summary>
    /// The provider's identifier for the series, or empty when it did not supply one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The series a match belongs to is knowable two ways, and they are not equally good.
    /// <see cref="SeriesName"/> is parsed out of a free-text field and is what a reader sees;
    /// this is an opaque id and is what a URL and a grouping key should use. Empty is expected
    /// rather than exceptional — a match can reach us without one, and such a match simply has
    /// no series page to belong to.
    /// </para>
    /// <para>
    /// <b>Deliberately not <c>required</c>, unlike everything around it.</b> This type is what the
    /// archive stores, as JSON, and a <c>required</c> member is a member that every row written
    /// before it existed now fails to deserialise. Adding one orphans history, which is the one
    /// thing the archive is for. Anything added here from now on needs a default for the same
    /// reason.
    /// </para>
    /// </remarks>
    public string SeriesId { get; init; } = string.Empty;

    public required string SeriesName { get; init; }

    public required string MatchTitle { get; init; }

    public required string Venue { get; init; }

    public required DateTimeOffset StartTimeUtc { get; init; }

    public required TeamInningsDto Home { get; init; }

    public required TeamInningsDto Away { get; init; }

    public required string StatusText { get; init; }
}
