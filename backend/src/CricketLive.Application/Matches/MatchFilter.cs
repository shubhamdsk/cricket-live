using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Application.Matches;

/// <summary>
/// Which matches a caller is asking about. Every field unset means "all of them".
/// </summary>
/// <remarks>
/// <para>
/// Matches live in two places — the provider's window in memory and the archive on disk — and the
/// same filter has to mean the same thing in both. Describing it once and applying it twice is
/// what keeps a LINQ predicate and a SQL <c>WHERE</c> from quietly disagreeing about, say, whether
/// a series name is matched case-sensitively.
/// </para>
/// <para>
/// The range is in UTC and half-open. Callers think in calendar days in their own timezone, which
/// is not a thing a server can infer: the same match starts on the 27th in London and the 28th in
/// Sydney. Converting a local day into an instant is therefore the caller's job, and this type
/// deliberately takes instants so there is nothing here to guess wrong.
/// </para>
/// </remarks>
/// <param name="Status">One status, or <see langword="null"/> for all three.</param>
/// <param name="FromUtc">Inclusive. Matches starting at or after this instant.</param>
/// <param name="ToUtc">Exclusive, so consecutive days abut without overlapping or leaving a gap.</param>
/// <param name="SeriesName">Compared whole and case-insensitively, never as a substring.</param>
public readonly record struct MatchFilter(
    MatchStatus? Status = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? SeriesName = null)
{
    /// <summary>Everything. The filter a caller who asked for nothing in particular gets.</summary>
    public static MatchFilter None => default;

    /// <summary>
    /// Whether this narrows anything at all, so a caller can skip work it would not change.
    /// </summary>
    public bool IsEmpty =>
        Status is null && FromUtc is null && ToUtc is null && string.IsNullOrWhiteSpace(SeriesName);

    /// <summary>
    /// Whether one match satisfies this filter.
    /// </summary>
    /// <remarks>
    /// The in-memory half of the pair, used against the provider's window. The archive cannot use
    /// this — it filters in SQL so it never reads rows it is about to discard — which is exactly
    /// why the two predicates are written next to each other and tested against the same cases.
    /// </remarks>
    public bool Matches(MatchDto match)
    {
        if (Status is { } status && match.Status != status)
        {
            return false;
        }

        if (FromUtc is { } from && match.StartTimeUtc < from)
        {
            return false;
        }

        if (ToUtc is { } to && match.StartTimeUtc >= to)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(SeriesName)
            || string.Equals(match.SeriesName, SeriesName, StringComparison.OrdinalIgnoreCase);
    }
}
