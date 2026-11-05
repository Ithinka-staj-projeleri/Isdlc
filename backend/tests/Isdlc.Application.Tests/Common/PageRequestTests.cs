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
    public void ApplyPaging_ReturnsTheRequestedPage()
    {
        var numbers = Enumerable.Range(1, 100).AsQueryable();

        var page = numbers.ApplyPaging(new PageRequest { Page = 3, PageSize = 20 }).ToList();

        Assert.Equal(Enumerable.Range(41, 20), page);
    }

    [Fact]
    public void ApplyPaging_LastPage_ReturnsOnlyTheRemainingItems()
    {
        var numbers = Enumerable.Range(1, 45).AsQueryable();

        var page = numbers.ApplyPaging(new PageRequest { Page = 3, PageSize = 20 }).ToList();

        Assert.Equal(Enumerable.Range(41, 5), page);
    }

    [Fact]
    public void ApplyPaging_PageAfterTheEnd_IsEmpty()
    {
        var numbers = Enumerable.Range(1, 10).AsQueryable();

        var page = numbers.ApplyPaging(new PageRequest { Page = 5, PageSize = 20 }).ToList();

        Assert.Empty(page);
    }
}
