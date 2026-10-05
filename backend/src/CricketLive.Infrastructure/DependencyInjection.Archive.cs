using CricketLive.Application.Matches;
using CricketLive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CricketLive.Infrastructure;

public static partial class DependencyInjection
{
    /// <summary>
    /// Registers the store that keeps finished matches after the provider's window moves on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two providers, chosen by the shape of the connection string rather than by a second
    /// setting that could disagree with it. SQLite for local runs, because it is a file and there
    /// is nothing to install; PostgreSQL for deployments, because the free hosts worth using will
    /// not keep a file and the archive is the one thing here that cannot be rebuilt. D-028.
    /// </para>
    /// <para>
    /// Inferring the provider is a small piece of cleverness and it is deliberate: a
    /// <c>Database:Provider</c> setting alongside a connection string is two facts that can
    /// contradict each other, and the failure would be at startup in production.
    /// </para>
    /// </remarks>
    private static void AddArchive(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Archive")
            ?? "Data Source=cricket-live.db";

        services.AddDbContext<CricketLiveDbContext>(options =>
        {
            if (IsSqlite(connection))
            {
                options.UseSqlite(connection);
            }
            else
            {
                // Retry, because a free-tier PostgreSQL host suspends its compute after a few
                // minutes idle and this site is idle most of the time. The pool hands out a
                // connection the server has already dropped, the first query fails, and the
                // second — after a wake that takes a moment — succeeds. Without this, the first
                // visitor after a quiet spell gets an error and everyone after them is fine,
                // which is the most annoying shape a bug can have.
                options.UseNpgsql(ToNpgsqlConnectionString(connection), npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null));
            }
        });

        services.AddScoped<IMatchArchive, SqlMatchArchive>();
        services.AddScoped<IWindowSnapshotStore, SqlWindowSnapshotStore>();
    }

    /// <summary>
    /// Whether this connection string is SQLite's.
    /// </summary>
    /// <remarks>
    /// SQLite's only required keyword is <c>Data Source</c>, and while PostgreSQL accepts that as
    /// a synonym for <c>Host</c> in theory, no host hands one out written that way: they are URIs
    /// or they name <c>Host=</c>. Checking for the URI scheme first means a Neon or Render
    /// connection string is never mistaken for a file path.
    /// </remarks>
    private static bool IsSqlite(string connection)
        => !connection.StartsWith("postgres", StringComparison.OrdinalIgnoreCase)
           && connection.Contains("Data Source", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Accepts either the URL a PostgreSQL host hands out or the key-value form Npgsql wants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every managed host — Neon, Render, Supabase — gives you a <c>postgresql://</c> URL, and
    /// every one-click integration pastes that URL into the environment for you. Npgsql does not
    /// parse it. It throws, and the exception text contains the whole connection string, so the
    /// first thing a failed deploy does is print the database password into a log.
    /// </para>
    /// <para>
    /// Translating here means the value from the host works unmodified, which is worth more than
    /// it looks: the alternative is a documented hand-conversion, done under deploy pressure, on
    /// a string containing a password.
    /// </para>
    /// </remarks>
    private static string ToNpgsqlConnectionString(string connection)
    {
        if (!connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connection;
        }

        var url = new Uri(connection);
        var credentials = url.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.IsDefaultPort ? 5432 : url.Port,
            Database = url.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credentials[0]),

            // Required rather than merely preferred, and not negotiable down: every host that
            // issues a URL is reached over the public internet.
            SslMode = SslMode.Require,
        };

        if (credentials.Length == 2)
        {
            builder.Password = Uri.UnescapeDataString(credentials[1]);
        }

        // The query string is deliberately not carried over. What hosts put there are libpq's
        // options, and the two Neon sends are already covered: sslmode is set above, and channel
        // binding is something Npgsql negotiates on its own. Copying them across blindly would
        // fail, because libpq spells them with underscores and Npgsql does not.
        return builder.ConnectionString;
    }

    /// <summary>
    /// Brings the archive's schema up to date, creating it if it is not there yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Migrating on startup suits a single instance and would not suit a cluster, where two
    /// instances racing the same migration is a real failure. The deployment is pinned to one
    /// instance for other reasons already — SQLite is gone but the provider call budget is still
    /// counted per process — so this holds for now and is listed in docs/deployment.md as
    /// something that stops holding the moment a second instance exists.
    /// </para>
    /// <para>
    /// The migrations are PostgreSQL's, because that is what deployments run and migrations are
    /// provider-specific SQL. A local SQLite file is built from the model instead. That is a real
    /// asymmetry and the honest reason for it is that keeping two migration sets means two
    /// projects, which is a lot of machinery for a scratch database that is deleted whenever it
    /// is inconvenient. The cost is that a model change which breaks a migration will not show up
    /// locally — it shows up on deploy. D-028.
    /// </para>
    /// </remarks>
    public static async Task MigrateArchiveAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CricketLiveDbContext>();

        if (context.Database.IsSqlite())
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        // After the schema and before the first request, because the scope flag decides what every
        // list shows and a stale one would serve cricket we do not cover. See ArchiveScopeRefresh
        // for why this runs every start rather than once.
        await ArchiveScopeRefresh.ApplyAsync(
            context,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<ArchiveScopeRefresh>(),
            cancellationToken);
    }
}
