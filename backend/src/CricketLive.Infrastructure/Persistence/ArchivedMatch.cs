namespace CricketLive.Infrastructure.Persistence;

/// <summary>
/// One finished match, exactly as the provider described it.
/// </summary>
/// <remarks>
/// <para>
/// The columns are the ones we sort, page and look up by. <see cref="Payload"/> is the whole
/// serialised match, and it — not the columns — is what gets returned to a caller.
/// </para>
/// <para>
/// That split is deliberate. Normalising a match into teams, innings and venues would mean
/// deciding today how every field maps, and any field we mapped carelessly would be silently lost
/// for good. Keeping the provider's own shape means an archived match reads back identical to a
/// live one, and a column can be promoted out of the payload later without a backfill.
/// </para>
/// </remarks>
internal sealed class ArchivedMatch
{
    /// <summary>Our match id, which is the provider's.</summary>
    public required string Id { get; set; }

    public required string Slug { get; set; }

    /// <summary>
    /// Kept for sorting and for the results list, which is ordered by when play began.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTime"/> in UTC rather than a <see cref="DateTimeOffset"/> because SQLite
    /// refuses to order by the latter. Nothing is lost: the offset-bearing value is in the payload,
    /// and this column exists only to sort and page by.
    /// </remarks>
    public required DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// The provider's series identifier, indexed so a series page can find its matches without
    /// reading every payload. Empty for matches archived before this column existed, and for any
    /// the provider sent without one.
    /// </summary>
    public required string SeriesId { get; set; }

    /// <summary>
    /// Indexed too, because filtering by series name predates the id and still has to work for
    /// rows that have no id.
    /// </summary>
    public required string SeriesName { get; set; }

    /// <summary>
    /// The two sides, as the slug ids a team page is addressed by, indexed so that page can find
    /// its matches without reading every payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Home and away are separate columns rather than one list because a match has exactly two
    /// sides and SQLite has no array type worth the trouble. Reading a team's matches therefore
    /// means a union of two index lookups, which is what <c>GetByTeamAsync</c> does.
    /// </para>
    /// <para>
    /// Unlike <see cref="SeriesId"/> these were backfilled when the columns were added: the
    /// payload already held the teams, so the migration extracted them with SQLite's
    /// <c>json_extract</c> rather than leaving history unreachable. That is the promise the
    /// payload-plus-columns split was made for.
    /// </para>
    /// </remarks>
    public required string HomeTeamId { get; set; }

    public required string AwayTeamId { get; set; }

    /// <summary>
    /// Kept beside the ids for the same reason <see cref="SeriesName"/> is: a list of teams needs
    /// something to display, and deriving a name back from a slug would mangle every side whose
    /// name is not plain words — "St Kitts &amp; Nevis" among them.
    /// </summary>
    public required string HomeTeamName { get; set; }

    public required string AwayTeamName { get; set; }

    /// <summary>The serialised <c>MatchDetailsDto</c>. The archive's actual content.</summary>
    public required string Payload { get; set; }

    /// <summary>
    /// When we first saw this match finished.
    /// </summary>
    /// <remarks>
    /// Not the same as <see cref="StartTimeUtc"/>, and worth keeping separately: it is the only
    /// record of how far back the archive really reaches, which matters because it accumulates
    /// forward rather than being backfilled.
    /// </remarks>
    public required DateTime ArchivedAtUtc { get; set; }
}
