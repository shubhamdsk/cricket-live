using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// Stores finished matches as the provider gave them, and reads them back unchanged.
/// </summary>
internal sealed class SqlMatchArchive(
    CricketLiveDbContext database,
    TimeProvider timeProvider,
    ILogger<SqlMatchArchive> logger) : IMatchArchive
{
    /// <summary>
    /// The framework's shared web defaults, which is also what ASP.NET Core serialises with, so a
    /// payload written today reads back through the same naming a client already expects.
    /// </summary>
    private static readonly JsonSerializerOptions Format = JsonSerializerOptions.Web;

    public async Task SaveFinishedAsync(
        IReadOnlyList<MatchDetailsDto> window,
        CancellationToken cancellationToken)
    {
        var finished = window
            .Where(match => match.Status == MatchStatus.Completed)
            .ToArray();

        if (finished.Length == 0)
        {
            return;
        }

        var ids = finished.Select(match => match.Id).ToArray();

        var known = await database.ArchivedMatches
            .Where(archived => ids.Contains(archived.Id))
            .Select(archived => archived.Id)
            .ToListAsync(cancellationToken);

        // A finished match cannot change, so one already held is left exactly as it was written.
        // Rewriting it would churn the table on every poll for no gain.
        var fresh = finished
            .Where(match => !known.Contains(match.Id))
            .Select(match => new ArchivedMatch
            {
                Id = match.Id,
                Slug = match.Slug,
                StartTimeUtc = match.StartTimeUtc.UtcDateTime,
                SeriesId = match.SeriesId,
                SeriesName = match.SeriesName,
                Payload = JsonSerializer.Serialize(match, Format),
                ArchivedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            })
            .ToArray();

        if (fresh.Length == 0)
        {
            return;
        }

        database.ArchivedMatches.AddRange(fresh);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Archived {Count} newly finished match(es)", fresh.Length);
    }

    public async Task<IReadOnlyList<MatchDto>> GetFinishedAsync(
        MatchFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var payloads = await Apply(filter)
            .OrderByDescending(archived => archived.StartTimeUtc)
            .Skip(skip)
            .Take(take)
            .Select(archived => archived.Payload)
            .ToListAsync(cancellationToken);

        return [.. payloads.Select(Deserialize).OfType<MatchDetailsDto>()];
    }

    public async Task<MatchDetailsDto?> GetAsync(string matchId, CancellationToken cancellationToken)
    {
        var payload = await database.ArchivedMatches
            .Where(archived => archived.Id == matchId || archived.Slug == matchId)
            .Select(archived => archived.Payload)
            .FirstOrDefaultAsync(cancellationToken);

        return payload is null ? null : Deserialize(payload);
    }

    public Task<int> CountFinishedAsync(MatchFilter filter, CancellationToken cancellationToken)
        => Apply(filter).CountAsync(cancellationToken);

    /// <remarks>
    /// Grouped in SQL over the indexed columns, so no payload is read to answer this. The archive
    /// only ever holds finished matches, which is why <see cref="SeriesTally.HasUnfinished"/> is
    /// flatly false here rather than computed.
    /// </remarks>
    public async Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(CancellationToken cancellationToken)
    {
        var rows = await database.ArchivedMatches
            .GroupBy(archived => new { archived.SeriesId, archived.SeriesName })
            .Select(group => new
            {
                group.Key.SeriesId,
                group.Key.SeriesName,
                Count = group.Count(),
                First = group.Min(archived => archived.StartTimeUtc),
                Last = group.Max(archived => archived.StartTimeUtc),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new SeriesTally
            {
                SeriesId = row.SeriesId,
                SeriesName = row.SeriesName,
                MatchCount = row.Count,
                FirstMatchUtc = new DateTimeOffset(row.First, TimeSpan.Zero),
                LastMatchUtc = new DateTimeOffset(row.Last, TimeSpan.Zero),
                HasUnfinished = false,
            })
        ];
    }

    public async Task<IReadOnlyList<MatchDto>> GetBySeriesAsync(
        string seriesId,
        CancellationToken cancellationToken)
    {
        // An empty id would otherwise collect every match archived before the column existed into
        // one nonsense series. No id means no series page, so there is nothing to look up.
        if (string.IsNullOrWhiteSpace(seriesId))
        {
            return [];
        }

        var payloads = await database.ArchivedMatches
            .Where(archived => archived.SeriesId == seriesId)
            .OrderBy(archived => archived.StartTimeUtc)
            .Select(archived => archived.Payload)
            .ToListAsync(cancellationToken);

        return [.. payloads.Select(Deserialize).OfType<MatchDetailsDto>()];
    }

    /// <summary>
    /// Narrows the query before it runs, so a filtered page is a full page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every clause here is on an indexed column. <see cref="MatchFilter.Status"/> is deliberately
    /// not one of them: the archive only ever holds completed matches, so status is either
    /// satisfied by every row or by none, and expressing that as a column would be storing the
    /// same value a few thousand times.
    /// </para>
    /// <para>
    /// Series is plain equality. Case-insensitivity comes from the column's collation rather than
    /// from the comparison, which is what lets it stay index-backed and match what
    /// <see cref="MatchFilter.Matches"/> does in memory. <c>LIKE</c> would have been the obvious
    /// alternative and is the wrong tool twice over: a series name containing <c>%</c> would
    /// become a wildcard, and its case sensitivity differs between SQLite and PostgreSQL.
    /// </para>
    /// </remarks>
    private IQueryable<ArchivedMatch> Apply(MatchFilter filter)
    {
        var query = database.ArchivedMatches.AsQueryable();

        if (filter.Status is { } status && status != MatchStatus.Completed)
        {
            // Asking the archive for live matches is a coherent question with an empty answer,
            // not a mistake. Answering it honestly beats ignoring the clause.
            return query.Where(_ => false);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(archived => archived.StartTimeUtc >= from.UtcDateTime);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(archived => archived.StartTimeUtc < to.UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(filter.SeriesName))
        {
            var series = filter.SeriesName;
            query = query.Where(archived => archived.SeriesName == series);
        }

        return query;
    }

    private MatchDetailsDto? Deserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<MatchDetailsDto>(payload, Format);
        }
        catch (JsonException exception)
        {
            // A payload we can no longer read means the DTO changed shape incompatibly. Dropping
            // the row from the result is better than failing the whole results page over one match.
            logger.LogError(exception, "An archived match could not be read back and was skipped");
            return null;
        }
    }
}
