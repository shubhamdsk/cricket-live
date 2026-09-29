using System.ComponentModel.DataAnnotations;
using CricketLive.Application.Matches;
using Microsoft.AspNetCore.Mvc;

namespace CricketLive.Api.Contracts;

/// <summary>
/// The filter query parameters, bound once and shared by every list endpoint.
/// </summary>
/// <remarks>
/// A binding type rather than three parameters repeated on three actions, so the names, the
/// validation and the meaning cannot drift between endpoints — and so adding a filter later is one
/// edit rather than four.
/// </remarks>
public sealed class MatchFilterQuery
{
    /// <summary>
    /// Restricts to one status. Redundant on <c>/live</c> and <c>/upcoming</c>, which already are
    /// one status, and accepted there anyway so a client can send one filter to all three.
    /// </summary>
    [FromQuery(Name = "status")]
    public MatchStatus? Status { get; init; }

    /// <summary>
    /// Inclusive lower bound on start time, as an instant.
    /// </summary>
    /// <remarks>
    /// An instant rather than a date because a calendar day is not the same interval everywhere,
    /// and the server has no way to know which day the caller meant. The client converts its own
    /// local day into a range; see docs/api.md.
    /// </remarks>
    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; init; }

    /// <summary>Exclusive upper bound, so consecutive days abut without overlapping.</summary>
    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// An exact series name, as returned by <c>GET /api/matches/series</c>.
    /// </summary>
    /// <remarks>
    /// Matched whole and case-insensitively, never as a substring: this is a filter, not a search.
    /// A partial name matching several tournaments would silently widen the result.
    /// </remarks>
    [FromQuery(Name = "series")]
    [StringLength(256)]
    public string? Series { get; init; }

    /// <summary>
    /// Converts to the domain filter, dropping a range the caller has inverted.
    /// </summary>
    /// <remarks>
    /// An inverted range selects nothing, which is a confusing thing to serve silently. It is a
    /// bad request, and <see cref="TryToFilter"/> says so rather than returning an empty list that
    /// looks like an answer.
    /// </remarks>
    public bool TryToFilter(out MatchFilter filter, out string? error)
    {
        if (From is { } from && To is { } to && from >= to)
        {
            filter = MatchFilter.None;
            error = "'from' must be earlier than 'to'.";
            return false;
        }

        filter = new MatchFilter(
            Status,
            From,
            To,
            string.IsNullOrWhiteSpace(Series) ? null : Series.Trim());

        error = null;
        return true;
    }
}
