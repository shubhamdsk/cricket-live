namespace CricketLive.Application.Series;

/// <summary>
/// What one source knows about one series: how many matches of it that source holds, and when
/// the first and last of them were played.
/// </summary>
/// <remarks>
/// <para>
/// A partial answer by design. Matches of a single series live in two places — the provider's
/// current window and our archive — and neither is a superset of the other, so a series is
/// described by combining a tally from each rather than by trusting one.
/// </para>
/// <para>
/// This is not a DTO and never reaches a caller. It is the shape the two sources agree to report
/// in so that merging them is addition rather than special cases.
/// </para>
/// </remarks>
public sealed record SeriesTally
{
    public required string SeriesId { get; init; }

    public required string SeriesName { get; init; }

    public required int MatchCount { get; init; }

    public required DateTimeOffset FirstMatchUtc { get; init; }

    public required DateTimeOffset LastMatchUtc { get; init; }

    /// <summary>True when this source holds a match of the series that has not finished.</summary>
    /// <remarks>Always false from the archive, which only ever holds finished matches.</remarks>
    public required bool HasUnfinished { get; init; }

    /// <summary>
    /// The two sources' views of one series, added together.
    /// </summary>
    /// <remarks>
    /// The longer name wins because the disagreements observed between matches of one series are
    /// truncations rather than genuinely different names, so the longer string is the more
    /// complete one rather than an arbitrary pick.
    /// </remarks>
    public SeriesTally Merge(SeriesTally other) => new()
    {
        SeriesId = SeriesId,
        SeriesName = other.SeriesName.Length > SeriesName.Length ? other.SeriesName : SeriesName,
        MatchCount = MatchCount + other.MatchCount,
        FirstMatchUtc = FirstMatchUtc <= other.FirstMatchUtc ? FirstMatchUtc : other.FirstMatchUtc,
        LastMatchUtc = LastMatchUtc >= other.LastMatchUtc ? LastMatchUtc : other.LastMatchUtc,
        HasUnfinished = HasUnfinished || other.HasUnfinished,
    };
}
