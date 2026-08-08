using SchoolManagement.Application.Common.Pagination;

namespace SchoolManagement.UnitTests.Application;

/// <summary>
/// Tests for the pagination contract. The hard page-size cap is the property that stops one request
/// asking the database for everything, so it is worth testing at the boundaries.
/// </summary>
public sealed class PageRequestTests
{
    [Fact]
    public void From_AppliesDefaultsWhenNothingSupplied()
    {
        var request = PageRequest.From(page: null, pageSize: null);

        request.Page.ShouldBe(PageRequest.FirstPage);
        request.PageSize.ShouldBe(PageRequest.DefaultPageSize);
    }

    [Fact]
    public void From_KeepsSuppliedValues()
    {
        var request = PageRequest.From(page: 3, pageSize: 50);

        request.Page.ShouldBe(3);
        request.PageSize.ShouldBe(50);
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 25, 50)]
    public void Skip_IsZeroBasedFromAOneBasedPage(int page, int pageSize, int expectedSkip)
    {
        // The classic off-by-one: pages are 1-based on the wire because the query string is public API,
        // but SKIP must be 0-based. Getting this wrong silently hides the first page of every list.
        var request = PageRequest.From(page, pageSize);

        request.Skip.ShouldBe(expectedSkip);
    }

    [Fact]
    public void Clamp_CapsPageSizeAtTheMaximum()
    {
        var request = PageRequest.From(page: 1, pageSize: PageRequest.MaxPageSize + 5_000);

        request.Clamp().PageSize.ShouldBe(PageRequest.MaxPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Clamp_RaisesAnInvalidPageToTheFirstPage(int page)
    {
        // A negative page would produce a negative SKIP, which PostgreSQL rejects outright — turning a
        // sloppy query string into a 500 rather than a sensible first page.
        var request = PageRequest.From(page, pageSize: 20);

        request.Clamp().Page.ShouldBe(PageRequest.FirstPage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Clamp_ReplacesANonPositivePageSizeWithTheDefault(int pageSize)
    {
        var request = PageRequest.From(page: 1, pageSize);

        request.Clamp().PageSize.ShouldBe(PageRequest.DefaultPageSize);
    }

    [Fact]
    public void Clamp_LeavesAValidRequestUnchanged()
    {
        var request = PageRequest.From(page: 2, pageSize: 20);

        request.Clamp().ShouldBe(request);
    }
}
