using CricketLive.Application.Series.Dtos;

namespace CricketLive.Application.Series;

/// <summary>
/// Supplies a points table for a series, when anything can.
/// </summary>
/// <remarks>
/// <para>
/// A seam rather than a direct call because standings are the one part of a series we cannot
/// derive and our primary provider does not publish. Whatever supplies them is therefore a second
/// source with its own terms, its own failure modes and its own reasons to be switched off, and
/// none of that belongs in <see cref="SeriesService"/>.
/// </para>
/// <para>
/// The contract is that an empty list means "no table available", never "this series has no
/// table" and never an error. Implementations swallow their own failures and return empty, so a
/// source going down costs the reader a section of the page rather than the page.
/// </para>
/// </remarks>
public interface ISeriesStandingsProvider
{
    Task<IReadOnlyList<StandingDto>> GetAsync(SeriesDto series, CancellationToken cancellationToken);
}

/// <summary>The answer when no standings source is configured, which is the default.</summary>
public sealed class NoSeriesStandingsProvider : ISeriesStandingsProvider
{
    public Task<IReadOnlyList<StandingDto>> GetAsync(SeriesDto series, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StandingDto>>([]);
}
