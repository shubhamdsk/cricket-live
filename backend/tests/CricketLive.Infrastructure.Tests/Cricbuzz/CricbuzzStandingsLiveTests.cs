using CricketLive.Application.Series;
using CricketLive.Application.Series.Dtos;
using CricketLive.Infrastructure.Cricbuzz;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CricketLive.Infrastructure.Tests.Cricbuzz;

/// <summary>
/// The one link the other tests cannot cover: that a live series page still resolves, fetches and
/// parses together.
/// </summary>
/// <remarks>
/// <para>
/// Opt-in, and it stays that way. This sends real traffic to a site whose <c>robots.txt</c>
/// disallows us — a decision taken deliberately for the running application (D-020), but one that
/// should not be made again by every CI run on every branch. Set
/// <c>CRICKET_LIVE_LIVE_TESTS=1</c> to run it.
/// </para>
/// <para>
/// It also asserts nothing about specific teams or points. Those change with every round, so
/// pinning them would produce a test that fails because cricket happened. The structure is what
/// is worth checking here; the numbers are checked against a captured page in
/// <see cref="CricbuzzPointsTableTests"/>.
/// </para>
/// </remarks>
public sealed class CricbuzzStandingsLiveTests
{
    private const string Switch = "CRICKET_LIVE_LIVE_TESTS";

    private static bool Enabled => Environment.GetEnvironmentVariable(Switch) == "1";

    [SkippableFact]
    public async Task A_multi_team_competition_resolves_and_yields_a_table()
    {
        Skip.IfNot(Enabled, $"Set {Switch}=1 to read Cricbuzz for real.");

        var standings = await Read("Ranji Trophy Elite 2026-27");

        Assert.NotEmpty(standings);
        Assert.All(standings, row => Assert.False(string.IsNullOrWhiteSpace(row.TeamName)));
        Assert.All(standings, row => Assert.True(row.Played >= row.Won + row.Lost + row.Tied + row.NoResult));
    }

    [SkippableFact]
    public async Task A_series_the_listing_does_not_name_is_declined_rather_than_guessed()
    {
        Skip.IfNot(Enabled, $"Set {Switch}=1 to read Cricbuzz for real.");

        // Loading some other tournament's table would be worse than loading none.
        Assert.Empty(await Read("A Tournament That Does Not Exist 2026"));
    }

    private static async Task<IReadOnlyList<StandingDto>> Read(string seriesName)
    {
        var options = Options.Create(new CricbuzzOptions { StandingsEnabled = true });

        using var client = new HttpClient
        {
            BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + '/'),
            Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds * 3),
        };

        // Honest, as everywhere else. No Referer and no Origin.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.Value.UserAgent);

        using var cache = new MemoryCache(new MemoryCacheOptions());

        var provider = new CricbuzzStandingsProvider(
            client,
            cache,
            options,
            NullLogger<CricbuzzStandingsProvider>.Instance);

        return await provider.GetAsync(
            new SeriesDto
            {
                Id = Guid.NewGuid().ToString(),
                Slug = "x",
                Name = seriesName,
                StartTimeUtc = DateTimeOffset.UtcNow,
                LastMatchUtc = DateTimeOffset.UtcNow,
                MatchCount = 1,
                IsOngoing = true,
            },
            CancellationToken.None);
    }
}
