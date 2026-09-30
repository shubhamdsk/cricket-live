using System.Buffers.Text;
using System.Text;

namespace CricketLive.Application.Media;

/// <summary>
/// Translates a provider's crest address into one on our own API, and back again.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of one line in CricketData's terms of use: <i>"Hot-linking of images we
/// serve is not allowed. Download the images we've provided and set up your own CDN ... Your
/// domain may get blacklisted if you do this."</i> Every crest on the site was an
/// <c>&lt;img src&gt;</c> aimed at their origin, so every visitor spent their bandwidth. The
/// stated penalty lands on the domain rather than the image, which would have taken the API down
/// with the pictures.
/// </para>
/// <para>
/// So no DTO carries a provider image address any more. It carries a path on our API, and
/// <c>CrestsController</c> fetches the bytes once and serves them from there.
/// </para>
/// <para>
/// The path is produced here, in <c>Application</c>, rather than in <c>ApiRoutes</c> alongside
/// every other route, because both sides of the translation need it and the mapper that writes it
/// into a DTO cannot see the API project.
/// </para>
/// </remarks>
public static class CrestUrl
{
    /// <summary>The route the images are served on, without a leading slash, as MVC wants it.</summary>
    public const string Path = "api/crests";

    private const string Prefix = "/" + Path + "/";

    /// <summary>
    /// Nothing outside this list is fetched, and nothing outside it is passed through either.
    /// </summary>
    /// <remarks>
    /// Both of the provider's image hosts, taken from its own responses. The list is the only
    /// thing standing between the route and a request to any address a caller names, so it is
    /// checked when a token is written <i>and</i> again when one is read.
    /// </remarks>
    private static readonly string[] Hosts = ["g.cricapi.com", "h.cricapi.com"];

    /// <summary>
    /// The path a client should ask us for, or <c>null</c> when there is no crest to serve.
    /// </summary>
    /// <param name="providerUrl">
    /// What the provider gave us, which is absent for most sides. May already be one of our own
    /// paths, since the archive stores whole DTOs and reads them back through here.
    /// </param>
    public static string? ToProxyPath(string? providerUrl)
    {
        if (string.IsNullOrWhiteSpace(providerUrl))
        {
            return null;
        }

        var trimmed = providerUrl.Trim();

        // Already ours, so leave it be. A match archived after this change is stored with the
        // path already rewritten and must survive a second pass unchanged.
        if (trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var source) || !IsAllowed(source))
        {
            // An unrecognised host is dropped rather than either passed through or proxied.
            // Passing it through is the hot-link we are removing; proxying it would turn the
            // route into an open relay for whatever address the provider chose to send. The
            // crest then falls back to the side's initials, which is already what the many teams
            // with no crest at all look like.
            return null;
        }

        return Prefix + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(source.AbsoluteUri));
    }

    /// <summary>
    /// The address behind a token, or <c>null</c> if it does not decode to one we will fetch.
    /// </summary>
    /// <remarks>
    /// The token arrives from the caller, so the allow-list is applied here and not only when the
    /// token was written. Without that, anyone could encode any address and have the deployment
    /// request it on their behalf, from inside the host's network.
    /// </remarks>
    public static Uri? ToSource(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string decoded;

        try
        {
            decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException)
        {
            return null;
        }

        return Uri.TryCreate(decoded, UriKind.Absolute, out var source) && IsAllowed(source)
            ? source
            : null;
    }

    private static bool IsAllowed(Uri source)
        => source.Scheme == Uri.UriSchemeHttps
           && Hosts.Contains(source.Host, StringComparer.OrdinalIgnoreCase);
}
