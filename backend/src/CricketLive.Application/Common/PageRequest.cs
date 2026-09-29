namespace CricketLive.Application.Common;

/// <summary>
/// A validated page of a list: what to skip, how much to take, and which page that turned out to be.
/// </summary>
/// <remarks>
/// A page number and a skip count are the same request written two ways, and converting between
/// them at each endpoint is how one of them ends up off by one. <see cref="From"/> is the only
/// conversion, and the bounds live here rather than in a controller's default arguments so the
/// service and the endpoint cannot disagree about what an unspecified page means.
/// </remarks>
public readonly record struct PageRequest(int Skip, int Take, int Page)
{
    /// <summary>What a caller gets when they do not ask for a particular size.</summary>
    public const int DefaultSize = 20;

    /// <summary>The most one request may read, whatever the caller asks for.</summary>
    public const int MaxSize = 50;

    /// <summary>
    /// Clamps a caller's page and size into something answerable.
    /// </summary>
    /// <remarks>
    /// Clamped rather than rejected: a page number past the end is a normal thing for a client to
    /// ask when the list shrank under it, and an empty page answers that honestly. An oversized
    /// <paramref name="pageSize"/> is not rejected either, because the caller does not get to
    /// decide how much we read and does not need an error to be told so.
    /// </remarks>
    public static PageRequest From(int page, int pageSize)
    {
        var take = Math.Clamp(pageSize, 1, MaxSize);
        var number = Math.Max(page, 1);

        return new PageRequest((number - 1) * take, take, number);
    }
}
