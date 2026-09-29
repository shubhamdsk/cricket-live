namespace CricketLive.Api;

/// <summary>
/// Every route prefix the API serves, written once.
/// </summary>
/// <remarks>
/// Matches are served by two controllers — one returning the response envelope and one streaming
/// frames — which is exactly the situation where the same prefix ends up typed twice and only one
/// copy gets changed. The frontend has the same list in <c>src/services/endpoints.ts</c>; these two
/// files are the only places a route is spelled out on either side.
/// </remarks>
internal static class ApiRoutes
{
    private const string Root = "api";

    public const string Health = $"{Root}/health";

    public const string Matches = $"{Root}/matches";

    public const string Series = $"{Root}/series";
}
