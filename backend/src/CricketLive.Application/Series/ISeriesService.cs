using CricketLive.Application.Series.Dtos;

namespace CricketLive.Application.Series;

public interface ISeriesService
{
    /// <summary>
    /// Every series we hold a match for, ongoing ones first and then most recently played.
    /// </summary>
    /// <remarks>
    /// That order rather than alphabetical because the list answers "what is on", and a
    /// tournament in progress is the thing a reader came for.
    /// </remarks>
    Task<IReadOnlyList<SeriesDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One series with its matches, or <see langword="null"/> when no match we hold belongs to it.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty series, because a series we hold nothing of is indistinguishable
    /// from one that does not exist, and claiming it exists but is empty would be a claim we
    /// cannot support.
    /// </remarks>
    Task<SeriesDetailsDto?> GetByIdAsync(string idOrSlug, CancellationToken cancellationToken);
}
