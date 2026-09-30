using CricketLive.Api;
using CricketLive.Api.Controllers;
using CricketLive.Api.Middleware;
using CricketLive.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicyName = "CricketLiveCors";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

// Outside development an empty allow-list is almost certainly a missing environment variable
// rather than a decision to serve nobody. Failing at startup makes that a deployment error someone
// sees immediately; carrying on would produce an API that answers every browser with a CORS
// failure and looks, from the frontend, like the backend is down.
if (allowedOrigins.Length == 0 && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins is empty. Set it to the frontend's origin — a wildcard is not an "
        + "option here, since the browser is the only client and it sends its origin.");
}

// Kestrel announces itself by default. The version of the server is of no use to a caller and of
// some use to someone scanning for a version with a known hole.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCricketLiveRateLimiter(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Fetches team crests so the browser does not, which CricketData's terms require. Deliberately
// plain: no key, no provider headers, nothing that would matter if the address were ever wrong.
builder.Services.AddHttpClient(
    CrestsController.ClientName,
    client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Whether a reverse proxy sits in front of us and may be believed about the original request.
// Off by default, and opt-in rather than detected, because the headers it enables are sent by the
// client and only a deployment can know whether something trustworthy overwrites them first.
var behindProxy = builder.Configuration.GetValue("ForwardedHeaders:Enabled", false);

if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // One hop. Anything beyond the proxy that terminates TLS is a header the client wrote,
        // and taking the leftmost entry is how X-Forwarded-For spoofing usually works.
        options.ForwardLimit = 1;

        // Platform proxies do not have stable addresses, so there is nothing to put on an
        // allow-list and the defaults (loopback only) would reject every real request. This is
        // the reason the whole block is opt-in: with it on, the app believes these headers from
        // anyone who can reach it directly. Only enable it where the platform is the sole route
        // in — on Fly, the internal port is not publicly reachable.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

await app.Services.MigrateArchiveAsync();

// Before anything reads the caller's address or the scheme.
//
// Rate limiting is the reason this exists. Partitions are keyed on the remote address, which
// behind a proxy is the proxy for every visitor, so without this the per-caller limit silently
// becomes one global cap shared by everybody.
//
// Not, as first assumed, to prevent a redirect loop: UseHttpsRedirection needs an HTTPS port to
// redirect to, the container binds only HTTP, so it finds none and does nothing. Measured, and
// written down in docs/deployment.md, because it is the kind of thing that gets re-assumed.
if (behindProxy)
{
    app.UseForwardedHeaders();
}

// First, so its headers are set before anything can write a response — including the live stream,
// whose headers go out with the first frame and cannot be changed afterwards.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Cricket Live API v1"));
}
else
{
    app.UseHttpsRedirection();

    // Only in production, and only after the redirect: sending HSTS from a development server
    // pins localhost to HTTPS in the developer's browser for the lifetime of the policy, which is
    // a confusing thing to debug and impossible to undo from the server side.
    app.UseHsts();
}

app.UseCors(CorsPolicyName);
app.UseRateLimiter();

// Liveness: is the process up and serving? Deliberately checks nothing else, so a dependency
// being down never causes an orchestrator to restart a process that is working fine.
app.MapHealthChecks(
    "/api/health/live",
    new HealthCheckOptions { Predicate = _ => false });

// Readiness: the archive and the provider allowance. Neither check calls the provider — see
// ProviderBudgetHealthCheck for why a probe that spends the resource it monitors is useless.
app.MapHealthChecks(
    "/api/health/ready",
    new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllers();

app.Run();
