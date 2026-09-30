namespace CricketLive.Api.Middleware;

/// <summary>
/// The response headers a JSON API should always send, and a few it should not.
/// </summary>
/// <remarks>
/// <para>
/// Set before the response starts rather than in <c>OnStarting</c>, because this runs first in the
/// pipeline and nothing has written yet. That matters for the live stream in particular: the
/// headers are in place before the first frame goes out, which is the only moment they can be.
/// </para>
/// <para>
/// A few well-known headers are deliberately absent. <c>Strict-Transport-Security</c> belongs to
/// <c>UseHsts</c>, which knows whether the request arrived over TLS. <c>Cross-Origin-Resource-Policy</c>
/// is omitted because this API is *meant* to be read from another origin — the frontend is deployed
/// separately — and <c>same-origin</c> would forbid exactly the case CORS is configured to allow.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // The one header with teeth on a JSON API: without it a browser may sniff a response
        // whose declared type it distrusts and execute it as something else.
        headers["X-Content-Type-Options"] = "nosniff";

        // Nothing here should ever leak a URL to a third party, and our URLs carry ids.
        headers["Referrer-Policy"] = "no-referrer";

        // Belt and braces with the CSP below. X-Frame-Options is obsolete but still honoured by
        // older browsers that ignore frame-ancestors.
        headers["X-Frame-Options"] = "DENY";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        if (ShouldRestrict(context))
        {
            // An API response is data. It loads nothing, frames nothing and is framed by nothing,
            // so the policy can be the strictest one there is rather than a list of exceptions.
            headers["Content-Security-Policy"] =
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        }

        return next(context);
    }

    /// <summary>
    /// Everything except Swagger UI in development, which is a real HTML page and needs to load
    /// its own scripts and styles. Sending it <c>default-src 'none'</c> would produce a blank page
    /// and an afternoon of confusion; it is not served outside development in any case.
    /// </summary>
    private bool ShouldRestrict(HttpContext context)
        => !environment.IsDevelopment()
            || context.Request.Path.StartsWithSegments("/api");
}
