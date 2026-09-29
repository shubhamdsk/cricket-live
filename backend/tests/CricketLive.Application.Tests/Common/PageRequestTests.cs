using CricketLive.Application.Common;

namespace CricketLive.Application.Tests.Common;

/// <summary>
/// The one conversion from a caller's page number to a skip count, so an off-by-one here is an
/// off-by-one everywhere.
/// </summary>
public sealed class PageRequestTests
{
    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 12, 24)]
    public void The_first_page_skips_nothing_and_each_page_skips_a_whole_page(
        int page,
        int size,
        int expectedSkip)
    {
        var request = PageRequest.From(page, size);

        Assert.Equal(expectedSkip, request.Skip);
        Assert.Equal(size, request.Take);
        Assert.Equal(page, request.Page);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_page_below_the_first_is_the_first(int page)
    {
        Assert.Equal(1, PageRequest.From(page, PageRequest.DefaultSize).Page);
        Assert.Equal(0, PageRequest.From(page, PageRequest.DefaultSize).Skip);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(PageRequest.MaxSize + 1, PageRequest.MaxSize)]
    [InlineData(int.MaxValue, PageRequest.MaxSize)]
    public void A_size_outside_the_bounds_is_brought_inside_them(int asked, int expected)
    {
        Assert.Equal(expected, PageRequest.From(1, asked).Take);
    }

    [Fact]
    public void The_reported_size_is_the_one_served_not_the_one_asked_for()
    {
        // Matters because the page is echoed back to the client, which pages by it.
        var request = PageRequest.From(3, 1_000);

        Assert.Equal(PageRequest.MaxSize, request.Take);
        Assert.Equal(PageRequest.MaxSize * 2, request.Skip);
    }

    [Fact]
    public void A_page_past_the_end_is_answerable_rather_than_an_error()
    {
        // A client asking for page 500 of a list that shrank gets an empty page, not a failure.
        var request = PageRequest.From(500, 20);

        Assert.Equal(9_980, request.Skip);
    }
}
