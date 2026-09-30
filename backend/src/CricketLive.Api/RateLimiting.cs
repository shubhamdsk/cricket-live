using System.Globalization;
using System.Threading.RateLimiting;
using CricketLive.Application.Common;
using CricketLive.Application.Media;

namespace CricketLive.Api;

/// <summary>
/// How much traffic one caller may send before we stop answering.
/// </summary>
/// <remarks>
/// <para>
/// Two limiters, because the two kinds of request fail differently. Ordinary endpoints are limited
/// by <b>rate</b> — a fixed window of requests per minute. The live stream is limited by
/// <b>concurrency</b>, because one stream is one request that stays open for as long as the match
/// lasts: counting it per minute measures nothing, while a client opening two hundred of them is
/// the actual abuse and costs us a connection each.
/// </para>
/// <para>
/// <b>Partitioned by remote IP, which behind a reverse proxy is the proxy.</b> Set
/// <c>ForwardedHeaders:Enabled</c> when deploying behind one, or every visitor shares a single
/// partition and these limits become one global cap. The failure is silent — the limiter keeps
/// working, it just protects the wrong thing — which is why the setting is called out in
/// <c>docs/deployment.md</c> rather than left to be noticed.
/// </para>
/// </remarks>
internal static class RateLimiting
{
    /// <summary>The concurrency policy for the SSE endpoint.</summary>
    public const string Stream = "cricket-live-stream";

    public static IServiceCollection AddCricketLiveRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var limits = configuration.GetSection("RateLimits").Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Crests are counted in their own bucket, and far more generously. A list page
                // asks for two of them per card, so counting them alongside everything else would
                // let a reader scrolling one full page spend the minute's allowance on pictures
                // and then find the API refusing to answer. They also cost us almost nothing: a
                // memory hit at worst, and usually a browser that never asks a second time.
                var images = context.Request.Path.StartsWithSegments("/" + CrestUrl.Path);

                return RateLimitPartition.GetFixedWindowLimiter(
                    (images ? "crest:" : "api:") + Client(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = images ? limits.ImagesPerMinute : limits.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),

                        // No queue. Holding a request to answer it a minute late is worse than
                        // refusing it now: the caller has already given up and we would spend a
                        // connection finding that out.
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy(Stream, context =>
                RateLimitPartition.GetConcurrencyLimiter(
                    Client(context),
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = limits.ConcurrentStreams,
                        QueueLimit = 0,
                    }));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                // Told how long to wait when the limiter knows, which is the difference between a
                // client that backs off correctly and one that retries in a tight loop.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                // The same envelope as every other response. A client that parses our errors
                // should not need a special case for this one.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail("Too many requests. Please slow down and try again."),
                    cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// The partition key: the caller's address, or one shared bucket when there is none.
    /// </summary>
    /// <remarks>
    /// A missing address is treated as a single shared partition rather than as unlimited. It
    /// happens for in-process requests and for some proxies, and "we could not identify you" is
    /// not a reason to stop counting.
    /// </remarks>
    private static string Client(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>Configured under <c>RateLimits</c>, with defaults that suit a small public API.</summary>
internal sealed class RateLimitOptions
{
    /// <summary>
    /// Requests per minute per caller.
    /// </summary>
    /// <remarks>
    /// Generous on purpose. A single page view fans out to several list calls, and a reader
    /// clicking through matches should never meet this; it exists to stop a script, not a person.
    /// </remarks>
    public int RequestsPerMinute { get; init; } = 120;

    /// <summary>
    /// Simultaneous live streams per caller.
    /// </summary>
    /// <remarks>
    /// A reader watches one match, occasionally a second in another tab. Four leaves room for a
    /// reconnect racing a stale connection that has not yet been cleaned up.
    /// </remarks>
    public int ConcurrentStreams { get; init; } = 4;

    /// <summary>
    /// Crest requests per minute per caller.
    /// </summary>
    /// <remarks>
    /// High because a crest is served from memory and cached by the browser as immutable, so the
    /// second page view asks for none of them. This is here to bound a script pulling every
    /// image we hold, not to shape ordinary reading.
    /// </remarks>
    public int ImagesPerMinute { get; init; } = 600;
}
