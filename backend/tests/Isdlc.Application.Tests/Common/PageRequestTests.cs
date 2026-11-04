using Isdlc.Application.Common.Models;

namespace Isdlc.Application.Tests.Common;

public class PageRequestTests
{
    [Fact]
    public void Defaults_AreFirstPageWithDefaultSize()
    {
        var request = new PageRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(PageRequest.DefaultPageSize, request.PageSize);
        Assert.Equal(0, request.Skip);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_BelowOne_IsRaisedToOne(int requested, int expected)
    {
        var request = new PageRequest { Page = requested };

        Assert.Equal(expected, request.Page);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(500, PageRequest.MaxPageSize)]
    public void PageSize_IsClampedToAllowedRange(int requested, int expected)
    {
        var request = new PageRequest { PageSize = requested };

        Assert.Equal(expected, request.PageSize);
    }

    [Fact]
    public void Skip_IsCalculatedFromPageAndPageSize()
    {
        var request = new PageRequest { Page = 3, PageSize = 20 };

        Assert.Equal(40, request.Skip);
    }
}
