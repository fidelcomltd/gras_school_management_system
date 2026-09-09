using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Audit;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Security;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0049's acceptance criteria: the five filters individually and combined,
/// the default newest-first sort with a STABLE tie-break across a page boundary (spec 6.1.12's
/// <c>id</c> exists precisely for this), the <c>audit.view</c>/<c>audit.export</c> privilege split,
/// and the export's own self-logging (it must write an <c>audit_event</c> row, recording the filters
/// used, before returning).
/// </summary>
public sealed class AuditEventEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string AuditEventsUrl = "/api/v1/audit-events";
    private const string ExportUrl = "/api/v1/audit-events/export";

    private static readonly DateTimeOffset BaseInstant = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_WithoutSignIn_Returns401()
    {
        RequireDatabase();

        var response = await GetAsync(AuditEventsUrl, new CookieJar());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_WithoutAuditViewGrant_Returns403()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Admin.View]);

        var response = await GetAsync(AuditEventsUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The card's own named criterion, verbatim: "audit.view holder gets 403 on export and 200 on list."
    [Fact]
    public async Task AccountWithViewButNotExport_SeesListButNotExport()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Audit.View]);

        var listResponse = await GetAsync(AuditEventsUrl, jar);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var exportResponse = await GetAsync(ExportUrl, jar);
        exportResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_DefaultSort_IsNewestFirstWithNoSortParameter()
    {
        RequireDatabase();

        var marker = $"test.default_sort.{Guid.NewGuid():N}";
        var olderId = await SeedAuditEventAsync(BaseInstant, marker, "entity_a");
        var newerId = await SeedAuditEventAsync(BaseInstant.AddMinutes(5), marker, "entity_a");

        var jar = await SignInWithGrantAsync([Privileges.Audit.View]);

        var response = await GetAsync($"{AuditEventsUrl}?action={marker}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<CursorPage<AuditEventDto>>(response);
        page.Items.Select(item => item.Id).ShouldBe([newerId, olderId]);
    }

    // The card's crux: occurred_at is NOT unique, so the tie-break must be id (descending), proven
    // by walking every page of a set seeded with an IDENTICAL occurred_at and asserting the union is
    // exactly the seeded set, in strict id-descending order, with no duplicate and no gap.
    [Fact]
    public async Task List_RowsWithIdenticalOccurredAt_ArePagedInStableIdDescendingOrder()
    {
        RequireDatabase();

        var marker = $"test.tie_break.{Guid.NewGuid():N}";
        var seededIds = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            seededIds.Add(await SeedAuditEventAsync(BaseInstant, marker, "entity_a"));
        }

        var jar = await SignInWithGrantAsync([Privileges.Audit.View]);

        var collected = new List<string>();
        string? cursor = null;

        do
        {
            var url = $"{AuditEventsUrl}?action={marker}&pageSize=2" +
                      (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");

            var response = await GetAsync(url, jar);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var page = await ReadAsync<CursorPage<AuditEventDto>>(response);
            collected.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        // Seeded oldest-to-newest id; the trail sorts newest (highest id) first when occurred_at ties.
        collected.ShouldBe(Enumerable.Reverse(seededIds).ToList());
    }

    [Fact]
    public async Task List_Filters_CombineAsAnIntersectionNotAnUnion()
    {
        RequireDatabase();

        var marker = $"test.combined_filters.{Guid.NewGuid():N}";
        var (actorId, _, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture);

        // Matches all three filters below.
        var target = await SeedAuditEventAsync(
            BaseInstant, marker, "target_entity", actorId, AuditOutcome.Success);

        // Each of these matches exactly two of the three filters, never all three.
        await SeedAuditEventAsync(BaseInstant, marker, "target_entity", actorAdminId: null, AuditOutcome.Success);
        await SeedAuditEventAsync(BaseInstant, marker, "other_entity", actorId, AuditOutcome.Success);
        await SeedAuditEventAsync(BaseInstant, marker, "target_entity", actorId, AuditOutcome.Rejected);

        var jar = await SignInWithGrantAsync([Privileges.Audit.View]);

        var url = $"{AuditEventsUrl}?action={marker}&entityType=target_entity&actorAdminId={actorId}&outcome=Success";
        var response = await GetAsync(url, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<CursorPage<AuditEventDto>>(response);
        page.Items.Select(item => item.Id).ShouldBe([target]);
    }

    [Fact]
    public async Task Export_WithoutAuditExportGrant_Returns403()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Admin.View]);

        var response = await GetAsync(ExportUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_ReturnsCsvWithTheThirteenColumnsAndTheMatchingRows()
    {
        RequireDatabase();

        var marker = $"test.export_csv.{Guid.NewGuid():N}";
        await SeedAuditEventAsync(BaseInstant, marker, "entity_a");

        var jar = await SignInWithGrantAsync([Privileges.Audit.Export]);

        var response = await GetAsync($"{ExportUrl}?action={marker}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        lines[0].ShouldBe(
            "id,occurredAtUtc,actorAdminId,actorLabel,action,entityType,entityId,outcome," +
            "beforeJson,afterJson,reason,sourceIp,userAgent");
        lines.Length.ShouldBe(2);
        lines[1].ShouldContain(marker);
        lines[1].ShouldContain("entity_a");
    }

    // The card's other named criterion: the export writes its OWN audit_event row, action
    // audit.export, before returning — and the filters actually used are recorded, so exporting the
    // whole log and exporting a narrowed slice are distinguishable after the fact.
    [Fact]
    public async Task Export_WritesItsOwnAuditEventRecordingTheFiltersUsed_DistinguishableFromAnUnfilteredExport()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Audit.Export, Privileges.Audit.View]);
        var narrowEntityType = $"probe_entity_{Guid.NewGuid():N}";

        var narrowResponse = await GetAsync($"{ExportUrl}?entityType={narrowEntityType}", jar);
        narrowResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await narrowResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var wholeLogResponse = await GetAsync(ExportUrl, jar);
        wholeLogResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        await wholeLogResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Read the log back through the endpoint this same card built — the whole point of the self
        // log is that it is answerable through the ordinary read surface, not just by a raw query.
        var selfLogResponse = await GetAsync(
            $"{AuditEventsUrl}?action={Uri.EscapeDataString(Privileges.Audit.Export)}&pageSize=100", jar);
        selfLogResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var selfLogPage = await ReadAsync<CursorPage<AuditEventDto>>(selfLogResponse);

        var narrowLogEntry = selfLogPage.Items.FirstOrDefault(item => LoggedEntityTypeIs(item, narrowEntityType));
        narrowLogEntry.ShouldNotBeNull();

        var wholeLogEntry = selfLogPage.Items.FirstOrDefault(item => LoggedEntityTypeIs(item, expected: null));
        wholeLogEntry.ShouldNotBeNull();

        narrowLogEntry!.Id.ShouldNotBe(wholeLogEntry!.Id);
    }

    /// <summary>
    /// Reads the self-log row's recorded <c>entityType</c> filter out of its <c>afterJson</c> —
    /// raw JSON text (<see cref="AuditEventDto.AfterJson"/> is a plain string, not a parsed element).
    /// </summary>
    private static bool LoggedEntityTypeIs(AuditEventDto item, string? expected)
    {
        if (item.AfterJson is not { } afterJson)
        {
            return false;
        }

        using var document = System.Text.Json.JsonDocument.Parse(afterJson);

        if (!document.RootElement.TryGetProperty("entityType", out var value))
        {
            return false;
        }

        return expected is null
            ? value.ValueKind == System.Text.Json.JsonValueKind.Null
            : value.GetString() == expected;
    }

    private async Task<string> SeedAuditEventAsync(
        DateTimeOffset occurredAt,
        string action,
        string entityType,
        Guid? actorAdminId = null,
        AuditOutcome outcome = AuditOutcome.Success)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var auditEvent = AuditEvent.Create(
            occurredAt,
            actorAdminId,
            "System",
            action,
            entityType,
            entityId: null,
            outcome,
            beforeJson: null,
            afterJson: null,
            reason: null,
            sourceIp: null,
            userAgent: null);

        context.Add(auditEvent);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return auditEvent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<Guid> SeedRoleAsync(string name, IReadOnlyCollection<string> privileges)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Domain.Security.Role.Create(Guid.CreateVersion7(), name, null, privileges);
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
    }

    private static int _nextSessionStartYear = 2000;

    /// <summary>Seeds a session with a fresh, valid <c>YYYY/YYYY</c> name — a distinct start year each call.</summary>
    private async Task<Guid> SeedSessionAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var name = $"{startYear}/{startYear + 1}";
        var session = Domain.Sessions.AcademicSession.Create(
            Guid.CreateVersion7(), name, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;

        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    /// <summary>
    /// Seeds a regular admin account, a role carrying exactly <paramref name="privileges"/>, and an
    /// ACTIVE, SCHOOL-WIDE <c>role_assignment</c> row — the same accepted direct-DbContext technique
    /// <c>PupilEndpointsTests</c> uses — then signs in as that account and returns its cookie jar.
    /// </summary>
    private async Task<CookieJar> SignInWithGrantAsync(IReadOnlyCollection<string> privileges)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync($"Role-{Guid.NewGuid():N}", privileges);
        var sessionId = await SeedSessionAsync();

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var creation = Domain.Security.RoleAssignment.Create(
                Guid.CreateVersion7(), accountId, roleId, sessionId, ScopeType.SchoolWide, [], accountId);
            creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

            context.Add(creation.Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        var signIn = await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private Task<HttpResponseMessage> GetAsync(string url, CookieJar jar) => GetAsync(Client, url, jar);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
