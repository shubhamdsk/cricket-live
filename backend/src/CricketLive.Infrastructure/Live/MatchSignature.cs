using System.Text;
using CricketLive.Application.Matches.Dtos;

namespace CricketLive.Infrastructure.Live;

/// <summary>
/// A comparable fingerprint of everything a watcher would notice changing.
/// </summary>
/// <remarks>
/// Record equality cannot do this job. <see cref="MatchDetailsDto"/> is a record, but its innings
/// are an <see cref="IReadOnlyList{T}"/>, and records compare list members by reference — a freshly
/// mapped response allocates new lists every time, so every poll would look like a change and every
/// client would be woken for nothing.
/// </remarks>
internal static class MatchSignature
{
    public static string For(MatchDetailsDto match)
    {
        var builder = new StringBuilder();

        builder.Append(match.Status).Append('|').Append(match.StatusText).Append('|');
        Append(builder, match.Home);
        builder.Append('|');
        Append(builder, match.Away);
        builder.Append('|');

        // A run scored off a single ball moves no other field here, so leaving the batters out
        // would mean the one thing enrichment exists to show never triggered a push.
        foreach (var batter in match.CurrentBatters)
        {
            builder
                .Append(batter.Name).Append(':')
                .Append(batter.Runs).Append('(')
                .Append(batter.Balls).Append(") ");
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, TeamInningsDto side)
    {
        foreach (var innings in side.Innings)
        {
            builder
                .Append(innings.Number).Append(':')
                .Append(innings.Runs).Append('/')
                .Append(innings.Wickets).Append('@')
                .Append(innings.Overs).Append(';');
        }
    }
}
