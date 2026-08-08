using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Exercises the reference vertical slice end to end against a real database.
/// </summary>
/// <remarks>
/// This is the test that proves the wiring: HTTP → versioned route → mediator → validation → handler →
/// repository → EF Core → PostgreSQL → auditing interceptor → result mapping → JSON. If this passes,
/// a new feature following the same shape will work.
/// </remarks>
public sealed class ReferenceSliceTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string PingUrl = "/api/v1/reference/ping?name=Ada";
    private const string RecordsUrl = "/api/v1/reference/records";

    [Fact]
    public async Task Ping_ReturnsOkWithTheEchoedName()
    {
        RequireDatabase();

        var response = await Client.GetAsync(new Uri(PingUrl, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await ReadAsync<PingResponse>(response);
        body.Message.ShouldBe("Hello, Ada.");
        body.ApiVersion.ShouldBe("1.0");
        body.ServerTimeUtc.Offset.ShouldBe(TimeSpan.Zero, "Timestamps must cross the wire in UTC.");
    }

    [Fact]
    public async Task Ping_WithAnEmptyName_Returns422WithPerFieldErrors()
    {
        RequireDatabase();

        // The validation contract: 422 (not 400) with an `errors` object a client can attach to fields.
        var response = await Client.GetAsync(
            new Uri("/api/v1/reference/ping?name=", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        root.GetProperty("errorCode").GetString().ShouldBe("request.validation_failed");
        root.GetProperty("errors").TryGetProperty(nameof(PingQuery.Name), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateRecord_Returns201WithALocationHeader()
    {
        RequireDatabase();

        var response = await Client.PostAsJsonAsync(
            new Uri(RecordsUrl, UriKind.Relative),
            new CreateSampleRecordCommand("Term 1 timetable", "A note."),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var body = await ReadAsync<CreateSampleRecordResponse>(response);
        body.Id.ShouldNotBeNullOrWhiteSpace();
        response.Headers.Location!.ToString().ShouldContain(body.Id);
    }

    [Fact]
    public async Task CreateRecord_PopulatesAuditFieldsViaTheInterceptor()
    {
        RequireDatabase();

        await CreateRecordAsync("Audited record");

        var listed = await GetPageAsync();
        var created = listed.Items.ShouldHaveSingleItem();

        // Set by AuditingInterceptor, not by the handler or the factory. A default value here would mean
        // the interceptor is not attached to the DbContext.
        created.CreatedAtUtc.ShouldNotBe(default);
        created.ModifiedAtUtc.ShouldBeNull("A freshly created row has never been modified.");
    }

    [Fact]
    public async Task CreateRecord_WithADuplicateLabel_Returns409()
    {
        RequireDatabase();

        await CreateRecordAsync("Duplicated label");

        var response = await Client.PostAsJsonAsync(
            new Uri(RecordsUrl, UriKind.Relative),
            new CreateSampleRecordCommand("Duplicated label", null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("sample_record.label_taken");
    }

    [Fact]
    public async Task CreateRecord_WithAnOverlongLabel_Returns422()
    {
        RequireDatabase();

        var response = await Client.PostAsJsonAsync(
            new Uri(RecordsUrl, UriKind.Relative),
            new CreateSampleRecordCommand(new string('a', 500), null),
            TestContext.Current.CancellationToken);

        // Rejected by the validator before the database sees it — a 422, not a truncation or a 500.
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ListRecords_ReturnsAnEmptyPageRatherThan404()
    {
        RequireDatabase();

        // "No rows matched" is a successful answer to a collection query. 404 is for a named resource.
        var page = await GetPageAsync();

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
        page.HasNextPage.ShouldBeFalse();
        page.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public async Task ListRecords_PaginatesInAStableOrder()
    {
        RequireDatabase();

        for (var index = 0; index < 5; index++)
        {
            await CreateRecordAsync($"Record {index:D2}");
        }

        var firstPage = await GetPageAsync(page: 1, pageSize: 2);
        var secondPage = await GetPageAsync(page: 2, pageSize: 2);

        firstPage.TotalCount.ShouldBe(5);
        firstPage.Items.Count.ShouldBe(2);
        firstPage.HasNextPage.ShouldBeTrue();
        firstPage.HasPreviousPage.ShouldBeFalse();

        secondPage.Items.Count.ShouldBe(2);
        secondPage.HasPreviousPage.ShouldBeTrue();

        // The ordering must be TOTAL, or paging can repeat a row on one page and skip another.
        var firstIds = firstPage.Items.Select(item => item.Id).ToArray();
        var secondIds = secondPage.Items.Select(item => item.Id).ToArray();

        firstIds.Intersect(secondIds, StringComparer.Ordinal).ShouldBeEmpty(
            "A row appeared on two pages, which means the query has no total ordering.");
    }

    [Fact]
    public async Task ListRecords_RejectsAPageSizeAboveTheCap()
    {
        RequireDatabase();

        // Rejected, not silently clamped: a client asking for 500 and receiving 100 without being told
        // would page incorrectly and skip four fifths of the data while believing it read everything.
        var response = await Client.GetAsync(
            new Uri($"{RecordsUrl}?pageSize={PageRequest.MaxPageSize + 1}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateRecord_RejectsUnknownJsonMembers()
    {
        RequireDatabase();

        // Unmapped members are rejected so a client that misspells a field is told, rather than getting a
        // 201 for a request that silently dropped the value.
        using var content = new StringContent(
            """{ "label": "Valid label", "nte": "misspelled" }""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync(
            new Uri(RecordsUrl, UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task CreateRecordAsync(string label)
    {
        var response = await Client.PostAsJsonAsync(
            new Uri(RecordsUrl, UriKind.Relative),
            new CreateSampleRecordCommand(label, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<PagedResult<SampleRecordDto>> GetPageAsync(int? page = null, int? pageSize = null)
    {
        var query = page is null && pageSize is null
            ? RecordsUrl
            : $"{RecordsUrl}?page={page ?? 1}&pageSize={pageSize ?? PageRequest.DefaultPageSize}";

        var response = await Client.GetAsync(new Uri(query, UriKind.Relative), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync<PagedResult<SampleRecordDto>>(response);
    }
}
