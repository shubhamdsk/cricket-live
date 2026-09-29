namespace CricketLive.Application.Enrichment;

/// <summary>
/// Just enough of a match for a second source to recognise it.
/// </summary>
/// <remarks>
/// Deliberately not the whole match. A supplementary source needs to answer "is this the same
/// fixture?" and nothing else, and handing it scores it might contradict invites exactly the
/// confusion this design is trying to avoid.
/// </remarks>
public readonly record struct MatchIdentity(
    string MatchId,
    string MatchTitle,
    string SeriesName,
    string HomeShortName,
    string AwayShortName);
