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

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Data = default, Message = message, Errors = errors };
}
