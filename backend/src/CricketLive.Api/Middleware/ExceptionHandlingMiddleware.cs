using System.Runtime.ExceptionServices;
using CricketLive.Application.Common;
using CricketLive.Application.Matches;

namespace CricketLive.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request cancelled by client: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        catch (CricketDataUnavailableException exception)
        {
            // Already logged with provider detail where it was thrown. The client is told only that
            // cricket data is unavailable, never which provider failed or why.
            logger.LogWarning(
                "Cricket data unavailable for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteEnvelopeAsync(
                context,
                StatusCodes.Status503ServiceUnavailable,
                "Live cricket data is temporarily unavailable. Please try again shortly.",
                exception);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteEnvelopeAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                exception);
        }
    }

    private static async Task WriteEnvelopeAsync(
        HttpContext context,
        int statusCode,
        string message,
        Exception exception)
    {
        // A streaming response (SSE) may already be mid-flight; the envelope can no longer be written.
        if (context.Response.HasStarted)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(message),
            context.RequestAborted);
    }
}
