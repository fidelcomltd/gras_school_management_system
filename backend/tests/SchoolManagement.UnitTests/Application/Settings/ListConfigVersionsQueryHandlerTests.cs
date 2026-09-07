using NSubstitute;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="ListConfigVersionsQueryHandler"/> — cursor pagination per spec 9.5.</summary>
public sealed class ListConfigVersionsQueryHandlerTests
{
    private readonly IConfigVersionRepository _repository = Substitute.For<IConfigVersionRepository>();

    private ListConfigVersionsQueryHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task HandleAsync_WithNoCursor_QueriesFromTheStart()
    {
        var page = new CursorPage<ConfigVersionSummaryDto>([], null);
        _repository.ListAsync(null, CursorPageRequest.DefaultPageSize, Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateHandler().HandleAsync(
            new ListConfigVersionsQuery(Cursor: null, PageSize: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).ListAsync(null, CursorPageRequest.DefaultPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAValidCursor_DecodesItBeforeQuerying()
    {
        var page = new CursorPage<ConfigVersionSummaryDto>([], null);
        _repository.ListAsync(41L, 10, Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateHandler().HandleAsync(
            new ListConfigVersionsQuery(Cursor: OpaqueCursor.Encode(41), PageSize: 10),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).ListAsync(41L, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAMalformedCursor_Returns422_AndNeverQueries()
    {
        var result = await CreateHandler().HandleAsync(
            new ListConfigVersionsQuery(Cursor: "not-a-valid-cursor!!", PageSize: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("config_versions.invalid_cursor");
        await _repository.DidNotReceiveWithAnyArgs().ListAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ClampsAPageSizeAboveTheMaximum()
    {
        var page = new CursorPage<ConfigVersionSummaryDto>([], null);
        _repository.ListAsync(null, CursorPageRequest.MaxPageSize, Arg.Any<CancellationToken>()).Returns(page);

        await CreateHandler().HandleAsync(
            new ListConfigVersionsQuery(Cursor: null, PageSize: 999),
            TestContext.Current.CancellationToken);

        await _repository.Received(1).ListAsync(null, CursorPageRequest.MaxPageSize, Arg.Any<CancellationToken>());
    }
}
