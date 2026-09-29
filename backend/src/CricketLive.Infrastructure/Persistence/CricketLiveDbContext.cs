using Microsoft.EntityFrameworkCore;

namespace CricketLive.Infrastructure.Persistence;

internal sealed class CricketLiveDbContext(DbContextOptions<CricketLiveDbContext> options)
    : DbContext(options)
{
    public DbSet<ArchivedMatch> ArchivedMatches => Set<ArchivedMatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var match = builder.Entity<ArchivedMatch>();

        match.ToTable("archived_matches");
        match.HasKey(entity => entity.Id);

        match.Property(entity => entity.Id).HasMaxLength(64);
        match.Property(entity => entity.Slug).HasMaxLength(256);
        // NOCASE so filtering by series is case-insensitive in SQL the same way it is in memory,
        // and stays index-backed while being so. This is the one provider-specific line in the
        // model: PostgreSQL spells the same idea as a citext column or a lower() index.
        match.Property(entity => entity.SeriesName).HasMaxLength(256).UseCollation("NOCASE");
        // An opaque provider id, so no collation: it is compared whole or not at all.
        match.Property(entity => entity.SeriesId).HasMaxLength(64);
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
    }
}
