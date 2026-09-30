using CricketLive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CricketLive.Infrastructure.Health;

/// <summary>
/// Can we reach the archive, and how much of it is there?
/// </summary>
/// <remarks>
/// <para>
/// A count rather than a connection test, because opening a SQLite file succeeds even when the
/// schema is wrong. Counting rows exercises the connection, the table and the migration together,
/// and it is one indexed aggregate rather than a scan.
/// </para>
/// <para>
/// An empty archive is <b>healthy</b>, not degraded. History accumulates forward from the day this
/// shipped, so zero rows is the correct state on a fresh deployment and reporting it as a fault
/// would make every new environment look broken.
/// </para>
/// </remarks>
internal sealed class ArchiveHealthCheck(CricketLiveDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var archived = await database.ArchivedMatches.CountAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                "Archive reachable.",
                new Dictionary<string, object> { ["archivedMatches"] = archived });
        }
        catch (Exception exception)
        {
            // The message is ours, not the exception's: a connection string or a file path in a
            // health payload is a detail a public endpoint should not hand out.
            return HealthCheckResult.Unhealthy("Archive unreachable.", exception);
        }
    }
}
