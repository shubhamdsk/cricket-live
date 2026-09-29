using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
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
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var payloads = await database.ArchivedMatches
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

    public Task<int> CountFinishedAsync(CancellationToken cancellationToken)
        => database.ArchivedMatches.CountAsync(cancellationToken);

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
