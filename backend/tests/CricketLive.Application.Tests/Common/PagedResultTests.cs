using CricketLive.Application.Common;

namespace CricketLive.Application.Tests.Common;

public sealed class PagedResultTests
{
    [Theory]
    [InlineData(1, 20, 100, true)]
    [InlineData(5, 20, 100, false)]
    [InlineData(4, 20, 100, true)]
    [InlineData(1, 20, 0, false)]
    [InlineData(1, 20, 20, false)]
    public void HasMore_is_true_only_while_matches_remain_beyond_this_page(
        int page,
        int size,
        int total,
        bool expected)
    {
        var result = new PagedResult<string>([], page, size, total);

        Assert.Equal(expected, result.HasMore);
    }

    [Fact]
    public void A_page_reports_the_size_that_was_served()
    {
        // Built from the request rather than from the caller's numbers, so a clamped size is not
        // echoed back as the one that was asked for.
        var result = PagedResult<string>.For(["a"], PageRequest.From(2, 1_000), 300);

        Assert.Equal(2, result.Page);
        Assert.Equal(PageRequest.MaxSize, result.PageSize);
        Assert.True(result.HasMore);
    }
}
