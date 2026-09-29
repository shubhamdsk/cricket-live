using System.Text.Json.Serialization;

namespace CricketLive.Application.Matches;

[JsonConverter(typeof(JsonStringEnumConverter<MatchFormat>))]
public enum MatchFormat
{
    /// <summary>The provider reported a format we do not model, such as a T10 or 100-ball game.</summary>
    [JsonStringEnumMemberName("OTHER")]
    Other,

    [JsonStringEnumMemberName("T20")]
    T20,

    [JsonStringEnumMemberName("ODI")]
    Odi,

    [JsonStringEnumMemberName("TEST")]
    Test
}
