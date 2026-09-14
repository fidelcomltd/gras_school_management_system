using System.Text;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Application.Audit;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.UnitTests.Api.Endpoints;

/// <summary>
/// TASK-0053: TASK-0049's RFC 4180 quoting is correct and does not change here, but quoting alone
/// does not stop formula interpretation — a quoted <c>"=1+1"</c> is still a formula when opened in
/// Excel, LibreOffice or Sheets. These prove the leading-apostrophe neutralisation fires for every
/// dangerous leading character, applies to every column (not an enumerated subset), runs BEFORE
/// RFC 4180 quoting, and leaves TASK-0049's own escaping behaviour unchanged.
/// </summary>
public sealed class AuditEventCsvWriterTests
{
    [Theory]
    [InlineData("=cmd", "'=cmd")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@sum", "'@sum")]
    public async Task UserAgent_LeadingDangerousCharacter_IsPrefixedWithAnApostrophe(string input, string expectedField)
    {
        var field = await ExportUserAgentFieldAsync(input);

        field.ShouldBe(expectedField);
    }

    [Fact]
    public async Task UserAgent_LeadingTab_IsPrefixedWithAnApostrophe()
    {
        var field = await ExportUserAgentFieldAsync("\tAAA");

        field.ShouldBe("'\tAAA");
    }

    [Fact]
    public async Task UserAgent_LeadingCarriageReturn_IsPrefixedThenStillRfc4180Quoted()
    {
        // CR is also one of TASK-0049's own four quoting triggers, so this is the one case where
        // both mechanisms fire on the same field — proving the ORDER: neutralise first, so the
        // apostrophe lands inside the quotes rather than corrupting them.
        var field = await ExportUserAgentFieldAsync("\rAAA");

        field.ShouldBe("\"'\rAAA\"");
    }

    [Fact]
    public async Task OrdinaryField_IsUnaffectedByNeutralisation()
    {
        var field = await ExportUserAgentFieldAsync("Mozilla/5.0");

        field.ShouldBe("Mozilla/5.0");
    }

    [Fact]
    public async Task Rfc4180Quoting_OfACommaContainingField_IsUnchanged()
    {
        var field = await ExportUserAgentFieldAsync("a,b");

        field.ShouldBe("\"a,b\"");
    }

    [Fact]
    public async Task Rfc4180Quoting_OfAnEmbeddedQuote_IsUnchanged()
    {
        var field = await ExportUserAgentFieldAsync("a\"b");

        field.ShouldBe("\"a\"\"b\"");
    }

    [Fact]
    public async Task Neutralisation_AppliesToEveryColumn_NotAnEnumeratedSubset()
    {
        var dto = new AuditEventDto(
            Id: "1",
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ActorAdminId: null,
            ActorLabel: "=label",
            Action: "action",
            EntityType: "entity",
            EntityId: null,
            Outcome: AuditOutcome.Success,
            BeforeJson: "=before",
            AfterJson: "=after",
            Reason: "=reason",
            SourceIp: null,
            UserAgent: "=agent");

        var line = await ExportSingleDataLineAsync(dto);
        var fields = line.Split(',');

        fields[3].ShouldBe("'=label");
        fields[8].ShouldBe("'=before");
        fields[9].ShouldBe("'=after");
        fields[10].ShouldBe("'=reason");
        fields[12].ShouldBe("'=agent");
    }

    private static async Task<string> ExportUserAgentFieldAsync(string userAgent)
    {
        var line = await ExportSingleDataLineAsync(BuildDto(userAgent));

        // userAgent is the thirteenth and last column. Splitting into at most 13 parts keeps any
        // comma the field itself carries inside the final part instead of fragmenting it.
        return line.Split(',', 13)[12];
    }

    private static AuditEventDto BuildDto(string userAgent) =>
        new(
            Id: "1",
            OccurredAtUtc: DateTimeOffset.UtcNow,
            ActorAdminId: null,
            ActorLabel: "actor",
            Action: "action",
            EntityType: "entity",
            EntityId: null,
            Outcome: AuditOutcome.Success,
            BeforeJson: null,
            AfterJson: null,
            Reason: null,
            SourceIp: null,
            UserAgent: userAgent);

    private static async Task<string> ExportSingleDataLineAsync(AuditEventDto dto)
    {
        await using var stream = new MemoryStream();

        await AuditEventCsvWriter.WriteAsync(stream, ToAsyncEnumerable(dto), CancellationToken.None);

        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        // Line 0 is the header, line 1 the one seeded row. Split on '\n' only — a bare '\r' inside
        // a quoted field (the CR test case) must not be mistaken for a line break.
        return body.Split('\n')[1].TrimEnd('\r');
    }

    private static async IAsyncEnumerable<AuditEventDto> ToAsyncEnumerable(AuditEventDto dto)
    {
        await Task.Yield();
        yield return dto;
    }
}
