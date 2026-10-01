namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// How far the match backfill has walked, and how much of today's allowance it has spent.
/// </summary>
/// <remarks>
/// <para>
/// One row, ever. It is in the database rather than in memory for a reason that is specific to
/// where this runs: the host spins the container down after a quarter of an hour of idleness and
/// rebuilds it on the next request, so an in-memory page counter would reset several times a day
/// and the daily cap would mean nothing. The offset would restart from zero too, and the backfill
/// would spend every day re-reading the same first pages.
/// </para>
/// <para>
/// This is bookkeeping, not cricket. Nothing reads it to answer a request.
/// </para>
/// </remarks>
internal sealed class BackfillState
{
    /// <summary>
    /// The only id this table uses.
    /// </summary>
    /// <remarks>
    /// A fixed key rather than an identity column, so "the state" is addressable without first
    /// asking what its id is, and so a second row cannot quietly come into existence.
    /// </remarks>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// The offset to read next, counted in matches rather than pages.
    /// </summary>
    /// <remarks>
    /// Approximate by nature, and that is tolerable. The provider's list is ordered by series with
    /// the newest first, not by date, and a new series appearing at the front shifts every offset
    /// behind it. So a lap can read a page twice or skip one. Reading twice costs nothing — the
    /// archive ignores a match it already holds — and a skipped page is picked up on the next lap.
    /// </remarks>
    public int NextOffset { get; set; }

    /// <summary>The UTC day <see cref="PagesReadToday"/> is counting, so it can be reset on a new one.</summary>
    public DateOnly PagesReadOn { get; set; }

    public int PagesReadToday { get; set; }

    /// <summary>
    /// How many times the backfill has walked its configured depth and started again.
    /// </summary>
    /// <remarks>
    /// Kept only to be logged. It is the one number that says whether the thing is making progress
    /// or quietly stuck on a page that always fails.
    /// </remarks>
    public int LapsCompleted { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
