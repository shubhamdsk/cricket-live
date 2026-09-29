namespace CricketLive.Infrastructure.CricketData;

/// <summary>
/// The provider answered, understood the request, and refused it. Distinct from being unreachable:
/// the provider reports both an unknown match and an exhausted key as HTTP 200 with a failure body,
/// and only the caller knows which of those is worth surfacing as "not found".
/// </summary>
internal sealed class CricketDataRejectedException(string? reason)
    : Exception($"The cricket data provider refused the request: {reason ?? "no reason given"}")
{
    public string Reason { get; } = reason ?? string.Empty;

    /// <summary>
    /// True when the refusal is about our account rather than the thing we asked for — an exhausted
    /// allowance or a bad key is an outage from the caller's point of view, not a missing match.
    /// </summary>
    public bool IsAccountProblem =>
        Reason.Contains("limit", StringComparison.OrdinalIgnoreCase)
        || Reason.Contains("key", StringComparison.OrdinalIgnoreCase)
        || Reason.Contains("subscription", StringComparison.OrdinalIgnoreCase);
}
