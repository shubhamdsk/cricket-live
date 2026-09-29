namespace CricketLive.Application.Matches;

/// <summary>
/// The cricket data provider failed. Carries no provider detail, so that whatever the API returns
/// to a browser cannot describe our upstream.
/// </summary>
public sealed class CricketDataUnavailableException : Exception
{
    public CricketDataUnavailableException(string message)
        : base(message)
    {
    }

    public CricketDataUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
