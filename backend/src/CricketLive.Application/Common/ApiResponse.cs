using System.Text.Json.Serialization;

namespace CricketLive.Application.Common;

/// <summary>
/// Envelope returned by every API endpoint so the frontend can rely on a single response shape.
/// </summary>
public sealed record ApiResponse<T>
{
    public required bool Success { get; init; }

    public T? Data { get; init; }

    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>
    /// When this data was read from the provider, present only when that was not just now.
    /// </summary>
    /// <remarks>
    /// Absent on almost every response, and that absence is the meaning: no field means the data is
    /// current. It appears when an endpoint has recovered a stored copy during a provider outage,
    /// so the page can say "as of 05:12" instead of presenting an old score as a live one.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? AsOfUtc { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Data = data, Message = message };

    /// <summary>Successful, but from a stored copy read at <paramref name="asOfUtc"/>.</summary>
    public static ApiResponse<T> Stale(T data, DateTimeOffset asOfUtc, string message = "Success") =>
        new() { Success = true, Data = data, Message = message, AsOfUtc = asOfUtc };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Data = default, Message = message, Errors = errors };
}
