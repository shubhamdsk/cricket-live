using CricketLive.Application.Scope;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// Recomputes <see cref="ArchivedMatch.InScope"/> for every stored match, at startup.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes the scope a policy rather than a property of a row.</b> The flag is
/// written when a match is archived, so without this it would record what the policy said on the
/// day the row arrived: widen the scope and the new cricket appears while the old stays hidden,
/// which is the kind of inconsistency that gets reported as missing data and found months later.
/// Recomputing on every start means the stored verdicts always agree with the code, and changing
/// the policy is a deploy and nothing else — no migration, no manual pass, no flag to remember.
/// </para>
/// <para>
/// It also has to exist for the column's first day. A new non-nullable boolean arrives as
/// <c>false</c> on every existing row, so the matches already archived would all read as
/// out-of-scope — the England Tests among them — until something worked out which were which.
/// </para>
/// <para>
/// <b>A full scan, and that is a decision with a lifetime.</b> Four narrow columns and no payload,
/// over an archive that is a few hundred rows and bounded by a backfill reading six pages a day,
/// so it is one query and a few milliseconds of comparisons. The alternative — a stored policy
/// version, scanned only when it changes — needs bumping by hand, and forgetting to bump it fails
/// silently in exactly the way this exists to prevent. If the archive ever reaches a size where a
/// startup scan is felt, that trade is the one to revisit, and it should move behind the first
/// response rather than in front of it.
/// </para>
/// </remarks>
internal sealed class ArchiveScopeRefresh
{
    public static async Task ApplyAsync(
        CricketLiveDbContext database,
        ILogger<ArchiveScopeRefresh> logger,
        CancellationToken cancellationToken)
    {
        // Projected rather than tracked. Nothing below needs an entity, and loading a few hundred
        // of them with their payloads to read three strings would be the expensive way to do this.
        var stored = await database.ArchivedMatches
            .Select(archived => new
            {
                archived.Id,
                archived.SeriesName,
                archived.HomeTeamName,
                archived.AwayTeamName,
                archived.InScope,
            })
            .ToListAsync(cancellationToken);

        if (stored.Count == 0)
        {
            return;
        }

        var changed = stored
            .Select(row => new
            {
                row.Id,
                row.InScope,
                Covered = CricketScope.IncludesMatch(
                    row.SeriesName,
                    row.HomeTeamName,
                    row.AwayTeamName),
            })
            .Where(row => row.Covered != row.InScope)
            .ToArray();

        if (changed.Length == 0)
        {
            logger.LogDebug("Archive scope unchanged across {Count} stored match(es)", stored.Count);
            return;
        }

        // Two statements at most, because the rows only ever move in one of two directions. Grouped
        // rather than updated one at a time so this is a pair of round trips instead of hundreds.
        foreach (var group in changed.GroupBy(row => row.Covered))
        {
            var ids = group.Select(row => row.Id).ToArray();

            await database.ArchivedMatches
                .Where(archived => ids.Contains(archived.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(archived => archived.InScope, group.Key),
                    cancellationToken);
        }

        logger.LogInformation(
            "Archive scope updated: {Shown} match(es) brought into scope, {Hidden} taken out, of {Total} stored",
            changed.Count(row => row.Covered),
            changed.Count(row => !row.Covered),
            stored.Count);
    }
}
