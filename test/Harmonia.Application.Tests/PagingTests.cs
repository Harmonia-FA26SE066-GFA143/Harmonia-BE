using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void PagingRequest_ClampsPageNumber(int input, int expected)
    {
        Assert.Equal(expected, new PagingRequest { PageNumber = input }.PageNumber);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(50, 50)]
    [InlineData(1000, 100)]
    public void PagingRequest_ClampsPageSize(int input, int expected)
    {
        Assert.Equal(expected, new PagingRequest { PageSize = input }.PageSize);
    }

    [Theory]
    [InlineData(1, 10, 25, 3, false, true)]
    [InlineData(3, 10, 25, 3, true, false)]
    [InlineData(1, 10, 0, 0, false, false)]
    [InlineData(1, 0, 5, 0, false, false)]
    public void PagedList_ComputesNavigation(
        int page, int size, int total, int expectedPages, bool expectedPrev, bool expectedNext)
    {
        var list = new PagedList<int>([], page, size, total);

        Assert.Equal(expectedPages, list.TotalPages);
        Assert.Equal(expectedPrev, list.HasPreviousPage);
        Assert.Equal(expectedNext, list.HasNextPage);
    }
}
