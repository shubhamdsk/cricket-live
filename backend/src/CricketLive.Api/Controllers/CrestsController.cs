using CricketLive.Application.Media;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CricketLive.Api.Controllers;

/// <summary>
/// Serves team crests from our own origin rather than the provider's.
/// </summary>
/// <remarks>
/// <para>
/// CricketData's terms forbid hot-linking the images it serves, and the remedy they name is to
/// serve them yourself. <see cref="CrestUrl"/> carries the reasoning and the exact wording.
/// </para>
/// <para>
/// <b>Not a general-purpose proxy.</b> The address is recovered from an opaque token and checked
/// against a host allow-list before anything is requested, so a caller cannot use this route to
/// reach an arbitrary URL from inside the deployment.
/// </para>
/// <para>
/// The cache is the whole point. Held in memory rather than on disk because the deployment has no
/// disk, which means a restart re-fetches — a handful of small images once, against one request
/// per visitor per page view, which is what the terms object to. The browser cache header does
/// most of the work in any case.
/// </para>
/// </remarks>
[ApiController]
[Route(CrestUrl.Path)]
public sealed class CrestsController(
    IHttpClientFactory clients,
    IMemoryCache cache,
    ILogger<CrestsController> logger) : ControllerBase
{
    /// <summary>The named client, kept plain: this route sends no key and no provider header.</summary>
    public const string ClientName = "Crests";

    /// <summary>
    /// A crest is a couple of kilobytes. Anything remotely near this is not one, and caching it
    /// would let an unexpected response from the host spend our memory for a week.
    /// </summary>
    private const int MaxBytes = 256 * 1024;

    private static readonly TimeSpan Held = TimeSpan.FromDays(7);

    /// <summary>
    /// Much shorter than a hit. A side whose crest is missing would otherwise cost a request to
    /// the provider on every page view that shows it, which is the problem in a new shape.
    /// </summary>
    private static readonly TimeSpan MissHeld = TimeSpan.FromHours(1);

    /// <summary>
    /// The crest behind a token, as image bytes. Not the response envelope every other route
    /// returns, because the caller here is an <c>&lt;img&gt;</c> rather than our own client.
    /// </summary>
    [HttpGet("{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string token, CancellationToken cancellationToken)
    {
        var source = CrestUrl.ToSource(token);

        if (source is null)
        {
            return NotFound();
        }

        if (!cache.TryGetValue($"crest:{token}", out Crest? crest))
        {
            crest = await FetchAsync(source, cancellationToken);
            cache.Set($"crest:{token}", crest, crest is null ? MissHeld : Held);
        }

        if (crest is null)
        {
            return NotFound();
        }

        // Immutable, because the provider's filenames carry their own version stamp: a changed
        // crest arrives as a different address and therefore a different token. This header is
        // what keeps the fix cheap — a reader fetches each crest once and never asks again.
        Response.Headers.CacheControl = "public, max-age=604800, immutable";

        return File(crest.Bytes, crest.ContentType);
    }

    private async Task<Crest?> FetchAsync(Uri source, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await clients
                .CreateClient(ClientName)
                .GetAsync(source, cancellationToken);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;

            // Only image bytes are passed on. Should the host ever answer with an error page
            // under HTTP 200, serving that to a browser as an image is the least useful thing we
            // could do with it.
            if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            // Checked again after reading, since a chunked response declares no length.
            return bytes.Length is 0 or > MaxBytes ? null : new Crest(bytes, contentType);
        }
        catch (HttpRequestException exception)
        {
            // Warned about rather than raised: the page renders the side's initials instead, which
            // it already does for every team the provider holds no crest for.
            logger.LogWarning(exception, "A team crest could not be fetched and will show as initials");
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Fetching a team crest timed out and it will show as initials");
            return null;
        }
    }

    private sealed record Crest(byte[] Bytes, string ContentType);
}
