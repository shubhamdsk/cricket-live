using System.Text.Json.Serialization;

namespace CricketLive.Application.Matches;

[JsonConverter(typeof(JsonStringEnumConverter<MatchStatus>))]
public enum MatchStatus
{
    [JsonStringEnumMemberName("upcoming")]
    Upcoming,

    [JsonStringEnumMemberName("live")]
    Live,

    [JsonStringEnumMemberName("completed")]
    Completed
}
