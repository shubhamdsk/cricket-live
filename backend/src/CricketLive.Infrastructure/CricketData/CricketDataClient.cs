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
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
