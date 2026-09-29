using CricketLive.Application.Matches;
using CricketLive.Application.Matches.Dtos;
using CricketLive.Infrastructure.Live;
using Microsoft.Extensions.Logging.Abstractions;

namespace CricketLive.Infrastructure.Tests.Live;

public class MatchBroadcasterTests
{
    private const string MatchId = "90ae280c-cb10-4d58-9bcc-ec95294819e6";
    private const string OtherMatchId = "68e5d7d2-06ce-41b5-be0c-4f25240eade9";

    [Fact]
    public void Nobody_is_watching_before_anyone_subscribes()
    {
        Assert.False(Broadcaster().HasSubscribers);
    }

    [Fact]
    public async Task A_subscriber_receives_an_update_for_its_match()
    {
        var broadcaster = Broadcaster();
        using var subscription = broadcaster.Subscribe(MatchId);

        broadcaster.Publish([Match(runs: 301)]);

        var received = await subscription.Reader.ReadAsync(Timeout());
        Assert.Equal(301, received.Home.Innings[0].Runs);
    }

    /// <summary>
    /// The exit criterion that justifies the whole architecture: the number of people watching has
    /// no bearing on how much work the publisher does, and one publish reaches all of them.
    /// </summary>
    [Fact]
    public async Task One_publish_reaches_every_client_watching_that_match()
    {
        var broadcaster = Broadcaster();
        var subscriptions = Enumerable.Range(0, 25).Select(_ => broadcaster.Subscribe(MatchId)).ToArray();

        broadcaster.Publish([Match(runs: 302)]);

        foreach (var subscription in subscriptions)
        {
            var received = await subscription.Reader.ReadAsync(Timeout());
            Assert.Equal(302, received.Home.Innings[0].Runs);
            subscription.Dispose();
        }
    }

    [Fact]
    public void A_subscriber_is_not_sent_other_matches()
    {
        var broadcaster = Broadcaster();
        using var subscription = broadcaster.Subscribe(MatchId);

        broadcaster.Publish([Match(id: OtherMatchId)]);

        Assert.False(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public void A_slug_cased_differently_still_finds_the_same_subscriber_list()
    {
        var broadcaster = Broadcaster();
        using var subscription = broadcaster.Subscribe(MatchId.ToUpperInvariant());

        broadcaster.Publish([Match()]);

        Assert.True(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public void Publishing_to_a_match_nobody_watches_is_harmless()
    {
        var broadcaster = Broadcaster();

        broadcaster.Publish([Match()]);

        Assert.False(broadcaster.HasSubscribers);
    }

    [Fact]
    public void Disposing_the_last_subscription_tells_the_poller_to_stop()
    {
        var broadcaster = Broadcaster();
        var first = broadcaster.Subscribe(MatchId);
        var second = broadcaster.Subscribe(MatchId);

        first.Dispose();
        Assert.True(broadcaster.HasSubscribers);

        second.Dispose();
        Assert.False(broadcaster.HasSubscribers);
    }

    [Fact]
    public void Disposing_twice_does_not_under_count_subscribers()
    {
        // A client that drops mid-write can reach disposal twice. Counting it twice would stop the
        // poller while other people are still watching.
        var broadcaster = Broadcaster();
        var leaving = broadcaster.Subscribe(MatchId);
        using var staying = broadcaster.Subscribe(MatchId);

        leaving.Dispose();
        leaving.Dispose();

        Assert.True(broadcaster.HasSubscribers);
    }

    [Fact]
    public void Repeated_open_and_close_cycles_leave_nothing_behind()
    {
        var broadcaster = Broadcaster();

        for (var i = 0; i < 500; i++)
        {
            broadcaster.Subscribe(MatchId).Dispose();
        }

        Assert.False(broadcaster.HasSubscribers);

        // And the registry is still usable rather than holding a stale empty entry.
        using var subscription = broadcaster.Subscribe(MatchId);
        broadcaster.Publish([Match()]);

        Assert.True(subscription.Reader.TryRead(out _));
    }

    [Fact]
    public async Task A_client_that_fell_behind_gets_the_current_score_not_a_backlog()
    {
        var broadcaster = Broadcaster();
        using var subscription = broadcaster.Subscribe(MatchId);

        broadcaster.Publish([Match(runs: 1)]);
        broadcaster.Publish([Match(runs: 2)]);
        broadcaster.Publish([Match(runs: 3)]);

        var received = await subscription.Reader.ReadAsync(Timeout());

        Assert.Equal(3, received.Home.Innings[0].Runs);
        Assert.False(subscription.Reader.TryRead(out _));
    }

    private static MatchBroadcaster Broadcaster() =>
        new(NullLogger<MatchBroadcaster>.Instance);

    private static CancellationToken Timeout() =>
        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

    private static MatchDetailsDto Match(string id = MatchId, int runs = 300) => new()
    {
        Id = id,
        Slug = $"india-vs-west-indies-{id}",
        Status = MatchStatus.Live,
        Format = MatchFormat.Odi,
        SeriesName = "West Indies tour of India, 2026",
        MatchTitle = "1st ODI",
        Venue = "Greenfield International Stadium",
        StartTimeUtc = DateTimeOffset.Parse("2026-09-27T08:30:00Z"),
        Home = new TeamInningsDto(
            new TeamDto("india", "India", "IND", null),
            [new InningsScoreDto(1, runs, 2, "41.4")]),
        Away = new TeamInningsDto(
            new TeamDto("west-indies", "West Indies", "WI", null),
            [new InningsScoreDto(1, 295, 7, "50")]),
        StatusText = "India need 5 runs",
        HasBallByBall = false,
        HasSquads = false,
    };
}
