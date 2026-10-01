using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Application.Media;
using CricketLive.Application.Scope;
using CricketLive.Application.Series;
using CricketLive.Application.Teams;
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

        // Deliberately not scoped. This asks what the table holds, not what the site shows, and an
        // out-of-scope row is still a row: scoping it would make every poll try to insert the same
        // primary key again.
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
                HomeTeamId = match.Home.Team.Id,
                AwayTeamId = match.Away.Team.Id,
                HomeTeamName = match.Home.Team.Name,
                AwayTeamName = match.Away.Team.Name,
                // Kept whether it is in scope or not, so widening the scope later does not mean
                // re-earning history at six provider pages a day. See ArchivedMatch.InScope.
                InScope = CricketScope.IncludesMatch(
                    match.SeriesName,
                    match.Home.Team.Name,
                    match.Away.Team.Name),
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
        var payload = await Covered
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
    public async Task<IReadOnlyList<SeriesTally>> GetSeriesTalliesAsync(
        IReadOnlyCollection<string> excluding,
        CancellationToken cancellationToken)
    {
        var rows = await Counting(excluding)
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

        var payloads = await Covered
            .Where(archived => archived.SeriesId == seriesId)
            .OrderBy(archived => archived.StartTimeUtc)
            .Select(archived => archived.Payload)
            .ToListAsync(cancellationToken);

        return [.. payloads.Select(Deserialize).OfType<MatchDetailsDto>()];
    }

    /// <remarks>
    /// <para>
    /// A side sits in the home column or the away one, so the two are unioned before being
    /// grouped — one query, still index-backed, and no payload read.
    /// </para>
    /// <para>
    /// Grouped by id with the name taken as an aggregate, the same way series are: the id is the
    /// identity and the name is only what we display, so two spellings of one side must not
    /// become two teams.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<TeamTally>> GetTeamTalliesAsync(
        IReadOnlyCollection<string> excluding,
        CancellationToken cancellationToken)
    {
        var counting = Counting(excluding);

        var sides = counting
            .Select(archived => new
            {
                Id = archived.HomeTeamId,
                Name = archived.HomeTeamName,
                archived.StartTimeUtc,
            })
            .Concat(counting
                .Select(archived => new
                {
                    Id = archived.AwayTeamId,
                    Name = archived.AwayTeamName,
                    archived.StartTimeUtc,
                }));

        var rows = await sides
            .Where(side => side.Id != string.Empty)
            .GroupBy(side => side.Id)
            .Select(group => new
            {
                TeamId = group.Key,
                Name = group.Max(side => side.Name),
                Count = group.Count(),
                First = group.Min(side => side.StartTimeUtc),
                Last = group.Max(side => side.StartTimeUtc),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new TeamTally
            {
                TeamId = row.TeamId,
                TeamName = row.Name ?? string.Empty,
                MatchCount = row.Count,
                FirstMatchUtc = new DateTimeOffset(row.First, TimeSpan.Zero),
                LastMatchUtc = new DateTimeOffset(row.Last, TimeSpan.Zero),
                HasUnfinished = false,
            })
        ];
    }

    public async Task<IReadOnlyList<MatchDto>> GetByTeamAsync(
        string teamId,
        CancellationToken cancellationToken)
    {
        // As with an empty series id: no id means no team page, so there is nothing to look up.
        if (string.IsNullOrWhiteSpace(teamId))
        {
            return [];
        }

        var payloads = await Covered
            .Where(archived => archived.HomeTeamId == teamId || archived.AwayTeamId == teamId)
            .OrderBy(archived => archived.StartTimeUtc)
            .Select(archived => archived.Payload)
            .ToListAsync(cancellationToken);

        return [.. payloads.Select(Deserialize).OfType<MatchDetailsDto>()];
    }

    /// <summary>
    /// The rows a tally should count: everything except what the caller already has in hand.
    /// </summary>
    /// <remarks>
    /// The exclusion list is the provider's window, a few dozen ids at most, so this is a short
    /// <c>NOT IN</c> rather than anything that needs a temporary table. An empty list adds no
    /// clause at all, which is the common case once the window and the archive stop overlapping.
    /// </remarks>
    private IQueryable<ArchivedMatch> Counting(IReadOnlyCollection<string> excluding)
        => excluding.Count == 0
            ? Covered
            : Covered.Where(archived => !excluding.Contains(archived.Id));

    /// <summary>
    /// The rows the site shows, which is where every read here starts.
    /// </summary>
    /// <remarks>
    /// One property rather than a clause repeated in six places, because the cost of forgetting it
    /// once is an out-of-scope match appearing on exactly one page — a series tally, say — and
    /// nowhere else, which reads as data corruption rather than as a missing filter. Index-backed
    /// via <c>ix_archived_matches_scope</c>. The write path does not go through here: see the note
    /// on the duplicate check in <see cref="SaveFinishedAsync"/>.
    /// </remarks>
    private IQueryable<ArchivedMatch> Covered
        => database.ArchivedMatches.Where(archived => archived.InScope);

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
        var query = Covered;

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
            var match = JsonSerializer.Deserialize<MatchDetailsDto>(payload, Format);

            return match is null ? null : WithOurOwnCrests(match);
        }
        catch (JsonException exception)
        {
            // A payload we can no longer read means the DTO changed shape incompatibly. Dropping
            // the row from the result is better than failing the whole results page over one match.
            logger.LogError(exception, "An archived match could not be read back and was skipped");
            return null;
        }
    }

    /// <summary>
    /// Points a stored match's crests at our own image route.
    /// </summary>
    /// <remarks>
    /// Rows written before hot-linking was removed hold the provider's absolute image address,
    /// because this class stores the DTO exactly as it was served and reads it back unchanged.
    /// Rewriting on the way out rather than migrating the table keeps the payload an honest
    /// record of what the provider said, and costs a few allocations per row. The translation is
    /// safe to apply twice, so a newer row passing through here again is left alone.
    /// </remarks>
    private static MatchDetailsDto WithOurOwnCrests(MatchDetailsDto match)
        => match with
        {
            Home = Rewritten(match.Home),
            Away = Rewritten(match.Away),
        };

    private static TeamInningsDto Rewritten(TeamInningsDto side)
        => side with { Team = side.Team with { LogoUrl = CrestUrl.ToProxyPath(side.Team.LogoUrl) } };
}
