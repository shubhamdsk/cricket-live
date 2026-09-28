namespace CricketLive.Api.Contracts;

public sealed record HealthStatusDto(string Status, string Environment, DateTimeOffset TimestampUtc);
