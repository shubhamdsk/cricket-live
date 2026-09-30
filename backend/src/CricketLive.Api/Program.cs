using CricketLive.Api;
using CricketLive.Api.Middleware;
using CricketLive.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

await app.Services.MigrateArchiveAsync();

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
