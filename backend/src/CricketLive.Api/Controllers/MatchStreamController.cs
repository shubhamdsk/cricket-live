using System.Text.Json;
using CricketLive.Application.Live;
using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.Live;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CricketLive.Api.Controllers;

/// <summary>
/// Server-Sent Events for one match. Separate from <see cref="MatchesController"/> because nothing
/// here uses the response envelope: the body is a stream of frames, not a JSON document.
/// </summary>
[ApiController]
[Route(ApiRoutes.Matches)]
public sealed class MatchStreamController(
    IMatchService matches,
    IMatchBroadcaster broadcaster,
    IOptions<LiveOptions> options,
    TimeProvider timeProvider,
    ILogger<MatchStreamController> logger) : ControllerBase
{
    /// <summary>
    /// The same shared web defaults MVC serialises the envelope endpoints with, so a match arriving
    /// over the stream is byte-identical to one fetched over <c>GET</c>.
    /// </summary>
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    /// <param name="matchId">Either the provider id or one of our slugs, which end in that id.</param>
    [HttpGet("{matchId}/stream")]
    public async Task Stream(string matchId, CancellationToken cancellationToken)
    {
        // Resolving first gives an honest 404 before we commit to a streaming response, and costs
        // nothing for a match already inside the provider window.
        var match = await matches.GetByIdAsync(matchId, cancellationToken);

        if (match is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        PrepareResponse();

        // Subscribe by the canonical id, not by whatever the caller typed, so a slug and a bare id
        // land on the same subscriber list.
        using var subscription = broadcaster.Subscribe(match.Id);

        await WriteEventAsync("match", match, cancellationToken);

        if (match.Status == MatchStatus.Completed)
        {
            // Nothing further can happen. Saying so lets the client close rather than hold a
            // connection open, and keeps the poller from running for a finished match.
            await WriteEventAsync("end", new { reason = "completed" }, cancellationToken);
            return;
        }

        await PumpAsync(subscription, cancellationToken);
    }

    private void PrepareResponse()
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";

        // Nginx and several free hosting proxies buffer responses by default, which turns a live
        // stream into one long silence. Task 8.26 exists to verify this survives deployment.
        Response.Headers["X-Accel-Buffering"] = "no";

        // Without this the server buffers frames too, and the first update arrives late or never.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    private async Task PumpAsync(MatchSubscription subscription, CancellationToken cancellationToken)
    {
        var heartbeat = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);
        var reader = subscription.Reader;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var next = reader.WaitToReadAsync(cancellationToken).AsTask();
                var beat = Task.Delay(heartbeat, timeProvider, cancellationToken);

                if (await Task.WhenAny(next, beat) == beat)
                {
                    // A comment frame. It carries nothing, and exists only so the proxy in the
                    // middle can see the connection is alive.
                    await Response.WriteAsync(": keepalive\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    continue;
                }

                if (!await next)
                {
                    break;
                }

                while (reader.TryRead(out var update))
                {
                    await WriteEventAsync("match", update, cancellationToken);

                    if (update.Status == MatchStatus.Completed)
                    {
                        await WriteEventAsync("end", new { reason = "completed" }, cancellationToken);
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The ordinary way a stream ends: the client navigated away or closed the tab.
            logger.LogDebug("Client disconnected from a match stream");
        }
    }

    private async Task WriteEventAsync<T>(string name, T payload, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(payload, Json);

        await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
