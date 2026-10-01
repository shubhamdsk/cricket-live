using Microsoft.EntityFrameworkCore;

namespace CricketLive.Infrastructure.Persistence;

internal sealed class CricketLiveDbContext(DbContextOptions<CricketLiveDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// The collation PostgreSQL uses for case-insensitive comparison, defined below.
    /// </summary>
    /// <remarks>
    /// Unlike SQLite's <c>NOCASE</c>, which is built in, this one has to be created in the
    /// database before a column can reference it. The model declares it so a migration creates
    /// it; there is nothing to remember at deploy time.
    /// </remarks>
    public const string PostgresCaseInsensitive = "case_insensitive";

    public DbSet<ArchivedMatch> ArchivedMatches => Set<ArchivedMatch>();

    public DbSet<BackfillState> BackfillState => Set<BackfillState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // SQLite's NOCASE only folds ASCII, and this one folds by Unicode rules, so a series name
        // with an accent in it compares slightly differently between a local run and a
        // deployment. Worth knowing, and not worth matching: the alternative is giving up
        // index-backed filtering on one side or the other.
        var postgres = Database.IsNpgsql();

        if (postgres)
        {
            // Non-deterministic, which is what makes 'a' and 'A' equal rather than merely sorted
            // together. level2 also ignores case while still respecting accents.
            builder.HasCollation(
                PostgresCaseInsensitive,
                locale: "und-u-ks-level2",
                provider: "icu",
                deterministic: false);
        }

        var caseInsensitive = postgres ? PostgresCaseInsensitive : "NOCASE";

        var match = builder.Entity<ArchivedMatch>();

        match.ToTable("archived_matches");
        match.HasKey(entity => entity.Id);

        match.Property(entity => entity.Id).HasMaxLength(64);
        match.Property(entity => entity.Slug).HasMaxLength(256);

        // Filtering by series is case-insensitive in SQL the same way it is in memory, and stays
        // index-backed while being so. This remains the one place the model has to know which
        // database it is talking to, because the two spell the same idea differently and neither
        // understands the other's spelling.
        match.Property(entity => entity.SeriesName).HasMaxLength(256).UseCollation(caseInsensitive);
        // An opaque provider id, so no collation: it is compared whole or not at all.
        match.Property(entity => entity.SeriesId).HasMaxLength(64);
        // Slugs, so already lowercase and compared whole: no collation needed. The names beside
        // them are display text and are never a lookup key, so they are not indexed either.
        match.Property(entity => entity.HomeTeamId).HasMaxLength(128);
        match.Property(entity => entity.AwayTeamId).HasMaxLength(128);
        match.Property(entity => entity.HomeTeamName).HasMaxLength(128);
        match.Property(entity => entity.AwayTeamName).HasMaxLength(128);
        match.Property(entity => entity.Payload).IsRequired();

        // The results list is "most recently played first" and nothing else, so this one index is
        // what every read of the archive actually uses.
        match.HasIndex(entity => entity.StartTimeUtc)
            .HasDatabaseName("ix_archived_matches_start_time");

        match.HasIndex(entity => entity.SeriesName)
            .HasDatabaseName("ix_archived_matches_series");

        // A series page reads its matches by id and shows them in playing order, so the sort
        // column belongs in the index rather than being applied to the rows it returns.
        match.HasIndex(entity => new { entity.SeriesId, entity.StartTimeUtc })
            .HasDatabaseName("ix_archived_matches_series_id");

        // One index per side, for the same reason: a team page reads its matches in playing order,
        // and a side appears in either column depending on who was at home.
        match.HasIndex(entity => new { entity.HomeTeamId, entity.StartTimeUtc })
            .HasDatabaseName("ix_archived_matches_home_team");

        match.HasIndex(entity => new { entity.AwayTeamId, entity.StartTimeUtc })
            .HasDatabaseName("ix_archived_matches_away_team");

        // Every read of the archive now begins with "and is it in scope", so the flag leads this
        // index and the sort column follows it — which is also the order the results list wants.
        // The other indexes above are deliberately left alone: adding the flag to each would widen
        // five indexes to narrow a set that a single lookup already narrows.
        match.HasIndex(entity => new { entity.InScope, entity.StartTimeUtc })
            .HasDatabaseName("ix_archived_matches_scope");

        var backfill = builder.Entity<BackfillState>();

        backfill.ToTable("backfill_state");
        // No generation strategy: the single row writes its own fixed key, so that the row can be
        // addressed before it exists rather than having to be looked up to be found.
        backfill.HasKey(entity => entity.Id);
        backfill.Property(entity => entity.Id).ValueGeneratedNever();
        // Unindexed, and correctly so. One row is read once every tick of a background loop and
        // never as part of answering a request.
    }
}
