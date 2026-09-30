using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CricketLive.Application.Matches;
using CricketLive.Infrastructure.CricketData.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// The single place an HTTP request leaves for the cricket data provider.
/// Claims budget, unwraps the provider envelope, and converts every failure into our own exception.
/// </summary>
internal sealed class CricketDataClient(
    HttpClient httpClient,
    IOptions<CricketDataOptions> options,
    CricketDataHitBudget budget,
    ILogger<CricketDataClient> logger)
{
    /// <summary>
    /// The framework's shared web defaults, which is also what ASP.NET Core serialises with. Not a
    /// private copy: every one of these in the solution wants the same behaviour, and a copy is
    /// only a place for them to drift apart.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    /// <summary>
    /// Says so when the provider had more rows than it sent, which is otherwise invisible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The provider pages its list endpoints and we ask for one page. <c>totalRows</c> is how it
    /// tells us the size of the whole answer, and until now it was parsed and thrown away — so a
    /// reader looking for a match that exists, on a page we never fetched, would find nothing
    /// and there would be no trace of why.
    /// </para>
    /// <para>
    /// A warning rather than a fix, deliberately. Fetching the next page costs another call from
    /// an allowance of a hundred a day that the poller already spends against, so whether it is
    /// worth paying depends on how often this actually fires. That is the number this line
    /// exists to produce. Silent when nothing is missing.
    /// </para>
    /// </remarks>
    private void WarnIfTruncated(string path, CricketDataInfo info, object? data)
    {
        if (data is not System.Collections.ICollection received || info.TotalRows <= received.Count)
        {
            return;
        }

        logger.LogWarning(
            "Cricket data {Path} returned {Received} of {TotalRows} rows; the rest are on pages we do not fetch",
            path,
            received.Count,
            info.TotalRows);
    }

    public async Task<T?> GetAsync<T>(
        string path,
        IReadOnlyDictionary<string, string>? query,
        CancellationToken cancellationToken)
    {
        if (!budget.TryClaim())
        {
            throw new CricketDataUnavailableException("The daily cricket data allowance has been used up.");
        }

        CricketDataEnvelope<T>? envelope;

        try
        {
            // The request URI carries the API key, so it is never logged. Only the path is.
            using var response = await httpClient.GetAsync(BuildRequestUri(path, query), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Cricket data provider returned {StatusCode} for {Path}",
                    (int)response.StatusCode,
                    path);

                throw new CricketDataUnavailableException("The cricket data provider rejected the request.");
            }

            envelope = await response.Content.ReadFromJsonAsync<CricketDataEnvelope<T>>(
                SerializerOptions,
                cancellationToken);
        }
        catch (ExecutionRejectedException exception)
        {
            // The resilience pipeline gave up before or during the call: an open circuit, or a
            // timeout it owns. No request reached the provider, so the claim goes back.
            budget.Refund();

            logger.LogWarning(
                "Cricket data request for {Path} was rejected by the resilience pipeline: {Reason}",
                path,
                exception.GetType().Name);

            throw new CricketDataUnavailableException("The cricket data provider is not responding.", exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Cricket data provider unreachable for {Path}", path);
            throw new CricketDataUnavailableException("The cricket data provider could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Cricket data provider timed out for {Path}", path);
            throw new CricketDataUnavailableException("The cricket data provider did not respond in time.", exception);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Cricket data provider returned unreadable JSON for {Path}", path);
            throw new CricketDataUnavailableException("The cricket data provider returned an unreadable response.", exception);
        }

        if (envelope is null)
        {
            throw new CricketDataUnavailableException("The cricket data provider returned an empty response.");
        }

        if (envelope.Info is { } info)
        {
            budget.Reconcile(info.HitsToday, info.HitsLimit);
            WarnIfTruncated(path, info, envelope.Data);
        }

        if (!envelope.IsSuccess)
        {
            // The provider reports refusals with HTTP 200 and a reason in the body. Whether that is
            // an outage or simply an unknown match is for the caller to decide.
            logger.LogWarning(
                "Cricket data provider refused {Path}: {Reason}",
                path,
                envelope.Reason);

            throw new CricketDataRejectedException(envelope.Reason);
        }

        return envelope.Data;
    }

    private Uri BuildRequestUri(string path, IReadOnlyDictionary<string, string>? query)
    {
        var builder = new StringBuilder(path)
            .Append("?apikey=")
            .Append(Uri.EscapeDataString(options.Value.ApiKey));

        foreach (var (key, value) in query ?? new Dictionary<string, string>())
        {
            builder
                .Append('&')
                .Append(Uri.EscapeDataString(key))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        return new Uri(builder.ToString(), UriKind.Relative);
    }
}
