namespace CricketLive.Api;

/// <summary>
/// Every route prefix the API serves, written once.
/// </summary>
/// <remarks>
/// <para>
/// Matches are served by two controllers — one returning the response envelope and one streaming
/// frames — which is exactly the situation where the same prefix ends up typed twice and only one
/// copy gets changed. The frontend has the same list in <c>src/services/endpoints.ts</c>; these two
/// files are the only places a route is spelled out on either side.
/// </para>
/// <para>
/// One route is missing from both, deliberately: crests live in
/// <see cref="CricketLive.Application.Media.CrestUrl"/>, because the mapper that writes the path
/// into a DTO cannot see this project. No client ever spells it — the path arrives inside a
/// response — so the pairing above still holds for everything a client asks for by name.
/// </para>
/// </remarks>
internal static class ApiRoutes
{
    private const string Root = "api";

    public const string Health = $"{Root}/health";

    public const string Matches = $"{Root}/matches";

    public const string Series = $"{Root}/series";

    public const string Teams = $"{Root}/teams";

    public const string Search = $"{Root}/search";
}
