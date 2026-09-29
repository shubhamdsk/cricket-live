namespace CricketLive.Application.Matches.Dtos;

/// <param name="Overs">Cricket over notation, where 12.3 means twelve overs and three balls.</param>
public sealed record InningsScoreDto(
    int Number,
    int Runs,
    int Wickets,
    string Overs);
