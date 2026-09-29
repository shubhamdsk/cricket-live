namespace CricketLive.Application.Common;

/// <summary>
/// One page of a longer list, with enough context for a client to know whether to ask for more.
/// </summary>
/// <remarks>
/// Sits inside the <c>ApiResponse</c> envelope rather than replacing it, so a paged endpoint still
/// reports success and errors the same way every other endpoint does.
/// </remarks>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total)
{
    /// <summary>
    /// Whether another page exists.
    /// </summary>
    /// <remarks>
    /// Computed here so every client does not re-derive it, and so an off-by-one is wrong in one
    /// place rather than several.
    /// </remarks>
    public bool HasMore => Page * PageSize < Total;

    /// <summary>
    /// Builds a page from the request that produced it, so the reported page and size are the ones
    /// actually served rather than the ones the caller asked for.
    /// </summary>
    public static PagedResult<T> For(IReadOnlyList<T> items, PageRequest page, int total)
        => new(items, page.Page, page.Take, total);
}
