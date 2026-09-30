using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.CricbuzzApi;

/// <summary>
/// The single place an HTTP request leaves for the RapidAPI gateway.
/// </summary>
/// <remarks>
/// <para>
/// Claims budget before the call and refunds it when nothing was spent, then reconciles against
/// the gateway's own remaining count. Returns <see langword="null"/> for every failure rather than
/// throwing: a scorecard is a supplement, and the match page it hangs off must not fail because of
/// it.
/// </para>
/// <para>
/// The key travels in a header, which is worth noting because our other provider has no such
/// option and puts it in the query string. A header keeps it out of URLs and therefore out of
/// anything that logs one — so the only discipline needed here is to not log the headers, rather
/// than to not log the request.
/// </para>
/// <para>
/// Deliberately no retry. At 200 calls a month a second attempt is a second call, and the thing it
/// would be retrying is somebody else's scraper having a bad moment.
/// </para>
/// </remarks>
internal sealed class CricbuzzApiClient(
    HttpClient httpClient,
    CricbuzzApiBudget budget,
    IOptions<CricbuzzApiOptions> options,
    ILogger<CricbuzzApiClient> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    /// <summary>
    /// Gets and deserialises one path, or returns null.
    /// </summary>
    /// <param name="forFinishedMatch">
    /// Whether the match has finished, which decides whether this call may use the reserve.
    /// </param>
    public async Task<T?> GetAsync<T>(
        string path,
        bool forFinishedMatch,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!budget.TryClaim(forFinishedMatch))
        {
            return null;
        }

        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken);

            Reconcile(response);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // The gateway's own cap, reached despite ours. Nothing to do but stop: retrying is
                // what turned a limit into a problem in the first place.
                logger.LogWarning("Cricbuzz API refused {Path}: the request quota is exhausted", path);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Cricbuzz API returned {StatusCode} for {Path}",
                    (int)response.StatusCode,
                    path);

                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            // Worth a warning rather than silence: this source is a scraper, so unreadable JSON is
            // a plausible symptom of the site it scrapes having changed shape.
            logger.LogWarning(exception, "Cricbuzz API returned unreadable JSON for {Path}", path);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up, so the call may still have been served. The claim stands.
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            budget.Refund();
            logger.LogWarning(exception, "Could not reach the Cricbuzz API for {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Takes the gateway's word for what is left.
    /// </summary>
    /// <remarks>
    /// Headers rather than a body field, and absent on an error response, so both are optional
    /// and a missing pair simply leaves our own count in place.
    /// </remarks>
    private void Reconcile(HttpResponseMessage response)
    {
        budget.Reconcile(
            Header(response, "x-ratelimit-requests-remaining"),
            Header(response, "x-ratelimit-requests-limit") ?? options.Value.MonthlyBudget);
    }

    private static int? Header(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values))
        {
            return null;
        }

        return int.TryParse(values.FirstOrDefault(), out var parsed) ? parsed : null;
    }
}
