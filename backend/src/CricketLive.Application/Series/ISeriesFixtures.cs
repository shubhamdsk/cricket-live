using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Series;

/// <summary>
/// Every match of one series as the provider lists them, including ones not yet played.
/// </summary>
/// <remarks>
/// <para>
/// The counterpart to <see cref="ISeriesIndex"/>, which says only that a series exists. This says
/// what is in it, and unlike the index it returns matches in the shape the rest of the application
/// already speaks — so a series page can show a fixture list rather than an apology for holding
/// two of eight matches.
/// </para>
/// <para>
/// Costs one provider call per series, which is why it is asked for a series at a time rather than
/// for all of them: the series list runs to sixty-odd entries and nobody opens sixty pages. A real
/// implementation caches, so repeated reads of one series inside a page load are free.
/// </para>
/// <para>
/// Failure returns empty for the same reason <see cref="ISeriesIndex"/> does. The matches we hold
/// are a complete answer on their own; these are an addition, and an addition that fails should
/// subtract nothing.
/// </para>
/// </remarks>
public interface ISeriesFixtures
{
    /// <summary>
    /// The series' matches, or empty when the provider has none or could not be read.
    /// </summary>
    /// <remarks>
    /// Empty is silence, never "this series has no matches". Callers merge this with what they
    /// hold and must let the held copy win, because ours carries scores and this one may not.
    /// </remarks>
    Task<IReadOnlyList<MatchDetailsDto>> GetAsync(string seriesId, CancellationToken cancellationToken);
}

/// <summary>Used when the fixture source is switched off, leaving held matches as the only source.</summary>
public sealed class NoSeriesFixtures : ISeriesFixtures
{
    public Task<IReadOnlyList<MatchDetailsDto>> GetAsync(string seriesId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<MatchDetailsDto>>([]);
}
