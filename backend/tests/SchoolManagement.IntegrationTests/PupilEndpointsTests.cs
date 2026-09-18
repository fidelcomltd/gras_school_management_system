using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// End-to-end proof of TASK-0050's two named criteria: the pending-exclusion invariant (spec 6.5.14)
/// enforced as a structural default rather than a per-caller filter, and arm-scoped <c>pupil.view</c>/
/// <c>pupil.update</c> proven against the REAL, DI-registered
/// <c>RoleAssignmentEffectivePrivilegeProvider</c> — never a fake, the same discipline
/// <c>AssignmentEndpointsTests</c> established. Plus the entity's own read/write surface: create,
/// list, get, biographical update, the 409 on a <c>registrationNumber</c> in the payload, the
/// admissions queue, duplicate detection and search.
/// </summary>
public sealed class PupilEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string PupilsUrl = "/api/v1/pupils";
    private const string AdmissionsUrl = "/api/v1/admissions";

    private static readonly DateOnly DefaultDateOfBirth = new(2020, 5, 3);

    [Fact]
    public async Task Create_HappyPath_Returns201WithPendingStatusAndNullRegistrationNumber()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);

        var response = await PostAsync(PupilsUrl, jar, await CreateCommandAsync("Okafor", "Chidera"), $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await ReadAsync<PupilDto>(response);
        body.Status.ShouldBe(PupilStatus.Pending);
        body.RegistrationNumber.ShouldBeNull();
        body.Surname.ShouldBe("Okafor");

        // Contract delta: "Response gains the same object" — section A's own fields round-trip.
        body.Admission.ShouldNotBeNull();
        body.Admission.AdmissionType.ShouldBe(AdmissionType.New);
        body.Admission.AssessmentRequired.ShouldBeFalse();
        body.Admission.DeclarationSigned.ShouldBeFalse();
        body.Admission.ApprovedBy.ShouldBeNull();
    }

    [Fact]
    public async Task Create_WithAnOutOfRangeDateOfBirth_Returns422WithTheVerbatimMessageShape()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var command = (await CreateCommandAsync("Okafor", "Chidera")) with { DateOfBirth = new DateOnly(2025, 1, 1) };

        var response = await PostAsync(PupilsUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.date_of_birth_out_of_range");
        var detail = document.RootElement.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldContain("makes this pupil");
        detail.ShouldContain("Check the date.");
    }

    [Fact]
    public async Task Create_WithFreeTextStateOfOrigin_Returns422()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var command = (await CreateCommandAsync("Okafor", "Chidera")) with { StateOfOrigin = "Not A Real State" };

        var response = await PostAsync(PupilsUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.state_of_origin_invalid");
    }

    [Fact]
    public async Task Create_WithoutSignIn_Returns401()
    {
        RequireDatabase();

        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);

        var response = await PostAsync(PupilsUrl, jar, await CreateCommandAsync("Okafor", "Chidera"), $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // The card's own named criterion: "GET /pupils without status returns no pending record even when
    // one matches every other filter, and GET /admissions returns it."
    [Fact]
    public async Task List_WithoutStatus_ExcludesAPendingPupil_ButAdmissionsQueueShowsIt()
    {
        RequireDatabase();

        var surname = "Excludedsurname";
        var pupilId = await SeedPupilDirectlyAsync(surname, "Chidera");

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var listResponse = await GetAsync($"{PupilsUrl}?search={surname}", jar);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listPage = await ReadAsync<CursorPage<PupilDto>>(listResponse);
        listPage.Items.ShouldNotContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));

        var pendingResponse = await GetAsync($"{PupilsUrl}?status=Pending&search={surname}", jar);
        pendingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pendingPage = await ReadAsync<CursorPage<PupilDto>>(pendingResponse);
        pendingPage.Items.ShouldContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));

        var admissionsResponse = await GetAsync(AdmissionsUrl, jar);
        admissionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var admissionsPage = await ReadAsync<CursorPage<PupilDto>>(admissionsResponse);
        admissionsPage.Items.ShouldContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Get_APendingPupilById_IsReachable_UnlikeTheList()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Reachable", "ById");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}/{pupilId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.Status.ShouldBe(PupilStatus.Pending);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // The other named criterion: arm-scoped pupil.view proven BOTH directions against the REAL
    // RoleAssignmentEffectivePrivilegeProvider. This seeds a PENDING pupil, which by construction
    // (spec 07 §6.5.11) never has an open enrolment, so an arm-scoped grant still matches no row —
    // TASK-0059's positive case (an arm-scoped caller reaching a pupil actually enrolled in their
    // arm) is List_ArmScopedCaller_SeesAPupilEnrolledInTheirArm_ExcludesOneInAnotherArm below. A
    // school-wide grant sees the record regardless, since it never consults an arm at all.
    [Fact]
    public async Task List_ArmScopedCaller_SeesAnEmptyPage_SchoolWideCallerSeesTheRecord()
    {
        RequireDatabase();

        var surname = "Scopeprobesurname";
        await SeedPupilDirectlyAsync(surname, "Chidera");

        var sessionId = await SeedSessionAsync();
        var armId = await SeedArmAsync(sessionId, "1A");

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [armId]);
        var schoolWideJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var armScopedResponse = await GetAsync($"{PupilsUrl}?status=Pending&search={surname}", armScopedJar);
        armScopedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var armScopedPage = await ReadAsync<CursorPage<PupilDto>>(armScopedResponse);
        armScopedPage.Items.ShouldBeEmpty();

        var schoolWideResponse = await GetAsync($"{PupilsUrl}?status=Pending&search={surname}", schoolWideJar);
        schoolWideResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var schoolWidePage = await ReadAsync<CursorPage<PupilDto>>(schoolWideResponse);
        schoolWidePage.Items.ShouldContain(item => item.Surname == surname);
    }

    // Same PENDING-pupil-has-no-open-enrolment shape as the list test above — the positive case is
    // Get_ArmScopedCaller_ReachesAPupilEnrolledInTheirArm_AndCannotReachOneInAnotherArm below.
    [Fact]
    public async Task Get_ArmScopedCaller_Returns403_SchoolWideCallerReturns200()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Scoped", "Target");

        var sessionId = await SeedSessionAsync();
        var armId = await SeedArmAsync(sessionId, "1B");

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [armId]);
        var schoolWideJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var armScopedResponse = await GetAsync($"{PupilsUrl}/{pupilId}", armScopedJar);
        armScopedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var schoolWideResponse = await GetAsync($"{PupilsUrl}/{pupilId}", schoolWideJar);
        schoolWideResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // TASK-0059: closes the drift the two tests above disclosed — arm-scoped pupil.view/update now
    // resolves a REAL arm from the pupil's open enrolment (spec 02 §5.2), so an arm-scoped caller
    // can reach a pupil actually enrolled in one of their granted arms, and still cannot reach one
    // enrolled in a DIFFERENT arm.
    [Fact]
    public async Task Get_ArmScopedCaller_ReachesAPupilEnrolledInTheirArm_AndCannotReachOneInAnotherArm()
    {
        RequireDatabase();

        var sessionId = await SeedSessionAsync();
        var grantedArmId = await SeedArmAsync(sessionId, "5A");
        var otherArmId = await SeedArmAsync(sessionId, "5B");

        var enrolledPupilId = await SeedPupilDirectlyAsync("Enrolled", "InGrantedArm");
        await SetRegistrationNumberAndStatusAsync(enrolledPupilId, "GRAS/2026/0050", PupilStatus.Active);
        await SeedEnrolmentAsync(enrolledPupilId, grantedArmId, new DateOnly(2026, 9, 1));

        var elsewherePupilId = await SeedPupilDirectlyAsync("Enrolled", "InOtherArm");
        await SetRegistrationNumberAndStatusAsync(elsewherePupilId, "GRAS/2026/0051", PupilStatus.Active);
        await SeedEnrolmentAsync(elsewherePupilId, otherArmId, new DateOnly(2026, 9, 1));

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [grantedArmId]);

        var reachableResponse = await GetAsync($"{PupilsUrl}/{enrolledPupilId}", armScopedJar);
        reachableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var unreachableResponse = await GetAsync($"{PupilsUrl}/{elsewherePupilId}", armScopedJar);
        unreachableResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_ArmScopedCaller_SeesAPupilEnrolledInTheirArm_ExcludesOneInAnotherArm()
    {
        RequireDatabase();

        var sessionId = await SeedSessionAsync();
        var grantedArmId = await SeedArmAsync(sessionId, "6A");
        var otherArmId = await SeedArmAsync(sessionId, "6B");

        var inArmSurname = "Instudentgrantedarm";
        var inArmPupilId = await SeedPupilDirectlyAsync(inArmSurname, "One");
        await SetRegistrationNumberAndStatusAsync(inArmPupilId, "GRAS/2026/0052", PupilStatus.Active);
        await SeedEnrolmentAsync(inArmPupilId, grantedArmId, new DateOnly(2026, 9, 1));

        var elsewhereSurname = "Instudentotherarm";
        var elsewherePupilId = await SeedPupilDirectlyAsync(elsewhereSurname, "Two");
        await SetRegistrationNumberAndStatusAsync(elsewherePupilId, "GRAS/2026/0053", PupilStatus.Active);
        await SeedEnrolmentAsync(elsewherePupilId, otherArmId, new DateOnly(2026, 9, 1));

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [grantedArmId]);

        var response = await GetAsync(PupilsUrl, armScopedJar);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<CursorPage<PupilDto>>(response);

        page.Items.ShouldContain(item => item.Id == inArmPupilId.ToString("D", CultureInfo.InvariantCulture));
        page.Items.ShouldNotContain(item => item.Id == elsewherePupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    // TASK-0061 (spec 6.5.15): default sort is level in progression order, then arm, then surname,
    // then id. Every seeded row's arm/surname is chosen so a WRONG tie-break (surname before arm, or
    // alphabetical level name instead of ProgressionOrder) would reorder the list — see
    // SeedRegisterOrderingFixtureAsync's own remarks for the six rows and why each is placed as it
    // is. Also proves the ruling's two other binding parts: unenrolled pupils (both shapes — a closed
    // enrolment and the pending→withdrawn lapsed application of 6.5.14) sort LAST as one flat block,
    // and are never sub-grouped by status inside it.
    [Fact]
    public async Task List_DefaultSort_OrdersByLevelThenArmThenSurname_AndSortsUnenrolledPupilsLastAsOneFlatBlock()
    {
        RequireDatabase();

        // No search marker needed for isolation: IntegrationTestBase.InitializeAsync truncates the
        // whole database before every test (ApiTestFixture.ResetDatabaseAsync), so this test's own
        // seeded rows are the only pupils in the table.
        const string Marker = "Regsort";
        var expectedOrder = await SeedRegisterOrderingFixtureAsync(Marker);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}?pageSize=50", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<CursorPage<PupilDto>>(response);

        page.Items.Select(item => item.Id).ShouldBe(
            expectedOrder.Select(id => id.ToString("D", CultureInfo.InvariantCulture)).ToArray());
        page.NextCursor.ShouldBeNull();
    }

    // The card's own named risk: widening the cursor is where an off-by-one page seam comes from. A
    // single-page test (above) cannot see a row repeated or skipped at a boundary, so this pages the
    // SAME six-row fixture two rows at a time, crossing the seam this card is most likely to get
    // wrong — including the one between the last enrolled row and the unenrolled sentinel block.
    [Fact]
    public async Task List_DefaultSort_PagesAcrossEverySeamWithoutRepeatingOrSkippingARow()
    {
        RequireDatabase();

        const string Marker = "Regseam";
        var expectedOrder = await SeedRegisterOrderingFixtureAsync(Marker);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var collected = new List<string>();
        string? cursor = null;

        do
        {
            var url = cursor is null
                ? $"{PupilsUrl}?pageSize=2"
                : $"{PupilsUrl}?pageSize=2&cursor={Uri.EscapeDataString(cursor)}";

            var response = await GetAsync(url, jar);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await ReadAsync<CursorPage<PupilDto>>(response);

            page.Items.Count.ShouldBeLessThanOrEqualTo(2);
            collected.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        collected.ShouldBe(expectedOrder.Select(id => id.ToString("D", CultureInfo.InvariantCulture)).ToArray());
    }

    [Fact]
    public async Task List_WithNoPupilViewGrantAtAll_Returns403()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Admin.View], ScopeType.SchoolWide);

        var response = await GetAsync(PupilsUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_SettingRegistrationNumber_Returns409()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Immutable", "Number");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);

        var response = await PatchAsync(
            $"{PupilsUrl}/{pupilId}",
            jar,
            new
            {
                registrationNumber = "GRAS/2026/0099",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.registration_number_immutable");
    }

    [Fact]
    public async Task Update_HappyPath_ChangesHomeAddressAndIsAudited()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var pupilId = await SeedPupilDirectlyAsync("Editable", "Pupil");
        var jar = await SignInWithGrantAsync(client, [Privileges.Pupil.Update], ScopeType.SchoolWide);

        var response = await PatchAsync(client, $"{PupilsUrl}/{pupilId}", jar, new { homeAddress = "9 New Road" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.HomeAddress.ShouldBe("9 New Road");

        auditSink.Records.ShouldContain(record =>
            record.Action == Privileges.Pupil.Update &&
            record.EntityId == pupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    // 6.5.3: "Ordinary pupil reads are NOT audited" — the card's own explicit instruction.
    [Fact]
    public async Task List_And_Get_AreNotAudited()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var pupilId = await SeedPupilDirectlyAsync("Unaudited", "Read");
        var jar = await SignInWithGrantAsync(client, [Privileges.Pupil.View], ScopeType.SchoolWide);

        await GetAsync(client, PupilsUrl, jar);
        await GetAsync(client, $"{PupilsUrl}/{pupilId}", jar);
        await GetAsync(client, AdmissionsUrl, jar);

        // Filtered to this card's own entity type — a background system job (idempotency-record
        // purge) may legitimately write its own, unrelated audit event during the same test run.
        auditSink.Records.ShouldNotContain(record => record.EntityType == "pupil");
    }

    // Spec 6.5.15's own worked example: typing the serial alone finds the full registration number.
    // The number is seeded directly (no issuance endpoint exists in this card — TASK-0051) purely to
    // prove the search MECHANISM, and the pupil is made ACTIVE so the ordinary list (no status filter)
    // finds it without needing the pending opt-out too.
    [Fact]
    public async Task Search_BySerialAlone_FindsTheFullRegistrationNumber()
    {
        RequireDatabase();

        var surname = "Serialsurname";
        var pupilId = await SeedPupilDirectlyAsync(surname, "Chidera");
        await SetRegistrationNumberAndStatusAsync(pupilId, "GRAS/2026/0041", PupilStatus.Active);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{PupilsUrl}?search=41", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync<CursorPage<PupilDto>>(response);
        var match = page.Items.Single(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));
        match.MatchedField.ShouldBe("RegistrationNumber");
    }

    [Fact]
    public async Task Duplicates_MatchesBySurnameFirstNameAndDateOfBirth_IncludingAPendingRecord()
    {
        RequireDatabase();

        var surname = "Duplicatesurname";
        var pupilId = await SeedPupilDirectlyAsync(surname, "Chidera", DefaultDateOfBirth);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);

        var response = await GetAsync(
            $"{PupilsUrl}/duplicates?surname={surname}&firstName=Chidera&dateOfBirth={DefaultDateOfBirth:yyyy-MM-dd}",
            jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var candidates = await ReadAsync<List<PupilDto>>(response);
        candidates.ShouldContain(item => item.Id == pupilId.ToString("D", CultureInfo.InvariantCulture));
    }

    // TASK-0062's own acceptance criterion: "a level with no arm in the session is ACCEPTED here and
    // blocks only at approval" — CreateCommandAsync's freshly-seeded session never has an arm.
    [Fact]
    public async Task Create_WithAClassLevelThatHasNoArmInTheSession_StillSaves()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);

        var response = await PostAsync(PupilsUrl, jar, await CreateCommandAsync("Noarm", "Inthesession"), $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_WithAnUnknownClassLevel_Returns422()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var baseCommand = await CreateCommandAsync("Unknown", "Level");
        var command = baseCommand with
        {
            Admission = baseCommand.Admission with
            {
                ClassAdmittedInto = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            },
        };

        var response = await PostAsync(PupilsUrl, jar, command, $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admission_record.class_admitted_into_invalid");
    }

    // The card's own acceptance criterion, proven end-to-end through the HTTP surface (the entity-level
    // proof lives in AdmissionRecordTests.Update_TwoDisjointPartialPayloads_BothSurvive).
    [Fact]
    public async Task PatchAdmission_TwoDisjointPartialPayloads_BothSurvive()
    {
        RequireDatabase();

        var createJar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var createResponse = await PostAsync(
            PupilsUrl, createJar, await CreateCommandAsync("Partial", "Payload"), $"key-{Guid.NewGuid():N}");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<PupilDto>(createResponse);

        var updateJar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);

        var firstResponse = await PatchAsync(
            $"{AdmissionsUrl}/{created.Id}", updateJar, new { admissionTypeNote = "Sibling of an existing pupil" });
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondResponse = await PatchAsync(
            $"{AdmissionsUrl}/{created.Id}",
            updateJar,
            new { declarationName = "Chinwe Okafor", declarationSigned = true, declarationDate = "2026-09-08" });
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync<AdmissionRecordDto>(secondResponse);

        updated.AdmissionTypeNote.ShouldBe("Sibling of an existing pupil");
        updated.DeclarationName.ShouldBe("Chinwe Okafor");
        updated.DeclarationSigned.ShouldBeTrue();
        updated.DeclarationDate.ShouldBe(new DateOnly(2026, 9, 8));
    }

    [Fact]
    public async Task PatchAdmission_ANonPendingPupil_Returns404()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Active", "AlreadyApproved");
        await SetRegistrationNumberAndStatusAsync(pupilId, "GRAS/2026/0060", PupilStatus.Active);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);

        var response = await PatchAsync(
            $"{AdmissionsUrl}/{pupilId}", jar, new { declarationSigned = true, declarationDate = "2026-09-08" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchAdmission_UnknownId_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);

        var response = await PatchAsync($"{AdmissionsUrl}/{Guid.CreateVersion7()}", jar, new { headOfSchoolConfirmed = true });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // TASK-0066: GET /admissions/{id} — the read twin of PATCH. Same id, same DTO shape (asserted by
    // id, not display name), same pending-only-404 rule, school-wide-only pupil.view, no write.
    [Fact]
    public async Task GetAdmission_HappyPath_ReturnsTheOpaqueSessionAndClassIds()
    {
        RequireDatabase();

        var createJar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var command = await CreateCommandAsync("Getadmission", "Happypath");
        var createResponse = await PostAsync(PupilsUrl, createJar, command, $"key-{Guid.NewGuid():N}");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<PupilDto>(createResponse);

        var readJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);
        var response = await GetAsync($"{AdmissionsUrl}/{created.Id}", readJar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<AdmissionRecordDto>(response);
        body.SessionId.ShouldBe(command.Admission.SessionId);
        body.ClassAdmittedInto.ShouldBe(command.Admission.ClassAdmittedInto);
    }

    [Fact]
    public async Task GetAdmission_AnApprovedPupil_Returns404()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Getadmission", "Approved");
        await SetRegistrationNumberAndStatusAsync(pupilId, "GRAS/2026/0070", PupilStatus.Active);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{AdmissionsUrl}/{pupilId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAdmission_ADeclinedPupil_Returns404()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Getadmission", "Declined");
        await SetRegistrationNumberAndStatusAsync(pupilId, "GRAS/2026/0071", PupilStatus.Withdrawn);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{AdmissionsUrl}/{pupilId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAdmission_UnknownId_Returns404()
    {
        RequireDatabase();

        var jar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);

        var response = await GetAsync($"{AdmissionsUrl}/{Guid.CreateVersion7()}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAdmission_CallerWithoutPupilView_Returns403()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Getadmission", "Forbidden");
        var jar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);

        var response = await GetAsync($"{AdmissionsUrl}/{pupilId}", jar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // A pending record has no arm, so an arm-scoped grant has no reach — matching
    // ListAdmissionsQueue's own school-wide-only decision rather than an honest empty page: a single
    // record read has no "empty," only a 404 that would lie about existence.
    [Fact]
    public async Task GetAdmission_ArmScopedPupilViewGrant_Returns403()
    {
        RequireDatabase();

        var pupilId = await SeedPupilDirectlyAsync("Getadmission", "Armscoped");
        var sessionId = await SeedSessionAsync();
        var armId = await SeedArmAsync(sessionId, "1G");

        var armScopedJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.ArmList, sessionId, [armId]);

        var response = await GetAsync($"{AdmissionsUrl}/{pupilId}", armScopedJar);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdmission_MatchesWhatPatchReturnsForTheSameRecord()
    {
        RequireDatabase();

        var createJar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var createResponse = await PostAsync(
            PupilsUrl, createJar, await CreateCommandAsync("Getadmission", "Matchespatch"), $"key-{Guid.NewGuid():N}");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<PupilDto>(createResponse);

        var updateJar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);
        var patchResponse = await PatchAsync(
            $"{AdmissionsUrl}/{created.Id}", updateJar, new { declarationName = "Chinwe Okafor" });
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patched = await ReadAsync<AdmissionRecordDto>(patchResponse);

        var readJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);
        var getResponse = await GetAsync($"{AdmissionsUrl}/{created.Id}", readJar);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await ReadAsync<AdmissionRecordDto>(getResponse);

        fetched.ShouldBe(patched);
    }

    [Fact]
    public async Task GetAdmission_WritesNothing_NoAuditRowAndNoModifiedAtBump()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var createJar = await SignInWithGrantAsync(client, [Privileges.Pupil.Create], ScopeType.SchoolWide);
        var createResponse = await PostAsync(
            client, PupilsUrl, createJar, await CreateCommandAsync("Getadmission", "Nowrite"), $"key-{Guid.NewGuid():N}");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<PupilDto>(createResponse);

        auditSink.Records.Clear();
        var modifiedBefore = await GetAdmissionRecordModifiedAtAsync(Guid.Parse(created.Id));

        var readJar = await SignInWithGrantAsync(client, [Privileges.Pupil.View], ScopeType.SchoolWide);
        var response = await GetAsync(client, $"{AdmissionsUrl}/{created.Id}", readJar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        auditSink.Records.ShouldBeEmpty();

        var modifiedAfter = await GetAdmissionRecordModifiedAtAsync(Guid.Parse(created.Id));
        modifiedAfter.ShouldBe(modifiedBefore);
    }

    private async Task<DateTimeOffset?> GetAdmissionRecordModifiedAtAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.AdmissionRecords.AsNoTracking()
            .Where(record => record.PupilId == pupilId)
            .Select(record => record.ModifiedAtUtc)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    // Spec 6.5.15's queue columns (TASK-0062): levelAppliedFor, dateApplicationReceived and missing.
    // missing here is restricted to what sections A and I's stored fields can check — see the
    // endpoint's own description for the recorded steps-3-to-8 gap.
    [Fact]
    public async Task AdmissionsQueue_RowsCarryLevelAppliedForDateApplicationReceivedAndMissing()
    {
        RequireDatabase();

        var createJar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var baseCommand = await CreateCommandAsync("Queuecolumn", "Surname");
        var command = baseCommand with
        {
            Admission = baseCommand.Admission with
            {
                AssessmentRequired = true,
                DateApplicationReceived = new DateOnly(2026, 8, 1),
            },
        };

        var createResponse = await PostAsync(PupilsUrl, createJar, command, $"key-{Guid.NewGuid():N}");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<PupilDto>(createResponse);

        var readJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);
        var queueResponse = await GetAsync(AdmissionsUrl, readJar);
        queueResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var queue = await ReadAsync<CursorPage<PupilDto>>(queueResponse);
        var row = queue.Items.Single(item => item.Id == created.Id);

        row.LevelAppliedFor.ShouldNotBeNullOrWhiteSpace();
        row.DateApplicationReceived.ShouldBe(new DateOnly(2026, 8, 1));
        row.Missing.ShouldNotBeNull();
        row.Missing.ShouldContain("Assessment result (Section A)");
        row.Missing.ShouldContain("Declaration (Section I)");
    }

    [Fact]
    public async Task AdmissionsQueue_MissingIsEmptyOnceAssessmentAndDeclarationAreRecorded()
    {
        RequireDatabase();

        var createJar = await SignInWithGrantAsync([Privileges.Pupil.Create], ScopeType.SchoolWide);
        var createResponse = await PostAsync(
            PupilsUrl, createJar, await CreateCommandAsync("Completesection", "Ai"), $"key-{Guid.NewGuid():N}");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync<PupilDto>(createResponse);

        var updateJar = await SignInWithGrantAsync([Privileges.Pupil.Update], ScopeType.SchoolWide);
        var patchResponse = await PatchAsync(
            $"{AdmissionsUrl}/{created.Id}",
            updateJar,
            new { declarationName = "Chinwe Okafor", declarationSigned = true, declarationDate = "2026-09-08" });
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var readJar = await SignInWithGrantAsync([Privileges.Pupil.View], ScopeType.SchoolWide);
        var queueResponse = await GetAsync(AdmissionsUrl, readJar);
        var queue = await ReadAsync<CursorPage<PupilDto>>(queueResponse);
        var row = queue.Items.Single(item => item.Id == created.Id);

        row.Missing.ShouldBeEmpty();
    }

    /// <summary>
    /// Seeds a session and reuses the first seeded class level, so <c>Admission</c> — REQUIRED since
    /// TASK-0062 — always references real rows. The session is passed EXPLICITLY (rather than left
    /// for the handler to default), so these tests never depend on an active session existing.
    /// </summary>
    private async Task<CreatePupilCommand> CreateCommandAsync(string surname, string firstName)
    {
        var sessionId = await SeedSessionAsync();
        var classLevelId = await GetFirstClassLevelIdAsync();

        return new CreatePupilCommand(
            surname,
            firstName,
            MiddleName: null,
            PupilSex.Female,
            DefaultDateOfBirth,
            Nationality: null,
            StateOfOrigin: "Anambra",
            Lga: "Awka South",
            HomeAddress: "14 Zik Avenue, Awka",
            PreviousSchool: null,
            PreviousClass: null,
            OtherInformation: null,
            Admission: new CreateAdmissionInput(
                SessionId: sessionId.ToString("D", CultureInfo.InvariantCulture),
                DateApplicationReceived: null,
                DateAdmitted: null,
                ClassAdmittedInto: classLevelId.ToString("D", CultureInfo.InvariantCulture),
                AdmissionType: AdmissionType.New,
                AdmissionTypeNote: null,
                AssessmentRequired: false));
    }

    private async Task<Guid> GetFirstClassLevelIdAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return (await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;
    }

    private async Task<Guid> SeedPupilDirectlyAsync(
        string surname, string firstName, DateOnly? dateOfBirth = null)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Pupil.Create(
            Guid.CreateVersion7(),
            surname,
            firstName,
            middleName: null,
            PupilSex.Female,
            dateOfBirth ?? DefaultDateOfBirth,
            asOfDate: new DateOnly(2026, 9, 9),
            nationality: null,
            "Anambra",
            "Awka South",
            "14 Zik Avenue, Awka",
            previousSchool: null,
            previousClass: null,
            otherInformation: null);

        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
    }

    /// <summary>
    /// Sets fields <see cref="Pupil"/> exposes no ordinary write path for — TEST SETUP standing in for
    /// TASK-0051's not-yet-built issuance, the same accepted direct-DbContext technique
    /// <c>AssignmentEndpointsTests.SeedAssignmentAsync</c> uses for its own otherwise-unreachable state.
    /// </summary>
    private async Task SetRegistrationNumberAndStatusAsync(Guid pupilId, string registrationNumber, PupilStatus status)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET registration_number = {registrationNumber}, status = {status.ToString()} WHERE id = {pupilId}",
            TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedRoleAsync(string name, IReadOnlyCollection<string> privileges)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var creation = Role.Create(Guid.CreateVersion7(), name, null, privileges);
        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        context.Add(creation.Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return creation.Value.Id;
    }

    private static int _nextSessionStartYear = 2000;

    /// <summary>Seeds a session with a fresh, valid <c>YYYY/YYYY</c> name — a distinct start year each call, so several calls within one test never collide.</summary>
    private async Task<Guid> SeedSessionAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var name = $"{startYear}/{startYear + 1}";
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), name, new DateOnly(startYear, 9, 14), new DateOnly(startYear + 1, 7, 25)).Value;

        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    private async Task<Guid> SeedArmAsync(Guid sessionId, string label)
    {
        var levelId = await GetFirstClassLevelIdAsync();
        return await SeedArmAsync(sessionId, label, levelId);
    }

    /// <summary>Same as <see cref="SeedArmAsync(Guid, string)"/>, at a caller-chosen level — TASK-0061's ordering tests need arms spread across more than one level.</summary>
    private async Task<Guid> SeedArmAsync(Guid sessionId, string label, Guid classLevelId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var arm = Arm.Create(Guid.CreateVersion7(), classLevelId, sessionId, label, null, null).Value;

        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }

    /// <summary>The seeded level (spec 6.4.2, <c>SeededClassLevels</c>) with this exact name — e.g. <c>"Primary 1"</c>.</summary>
    private async Task<Guid> GetClassLevelIdByNameAsync(string name)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.Name == name, TestContext.Current.CancellationToken)).Id;
    }

    /// <summary>
    /// Seeds an OPEN enrolment directly through the DbContext — TASK-0051's admission-approval flow
    /// does not exist yet to do this through a route, the same accepted technique this file already
    /// uses for status and registration-number setup.
    /// </summary>
    private async Task<Guid> SeedEnrolmentAsync(Guid pupilId, Guid armId, DateOnly effectiveFrom)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupilId, armId, effectiveFrom).Value;

        context.Add(enrolment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return enrolment.Id;
    }

    /// <summary>Closes a previously-seeded OPEN enrolment — spec 6.5.14's transfer/withdrawal/graduation effect, for a pupil TASK-0061's ordering tests need to land in the unenrolled trailing block.</summary>
    private async Task CloseEnrolmentAsync(Guid enrolmentId, DateOnly effectiveTo)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var enrolment = await context.Enrolments.FirstAsync(
            e => e.Id == enrolmentId, TestContext.Current.CancellationToken);

        var closeResult = enrolment.Close(effectiveTo);
        closeResult.IsSuccess.ShouldBeTrue(closeResult.IsFailure ? closeResult.Error.Description : string.Empty);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Sets ONLY status, registration number left null — the pending→withdrawn lapsed application of
    /// spec 6.5.14, which never held an enrolment and to which no registration number is ever issued.
    /// Same accepted direct-DbContext technique as <see cref="SetRegistrationNumberAndStatusAsync"/>.
    /// </summary>
    private async Task SetStatusAsync(Guid pupilId, PupilStatus status)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {status.ToString()} WHERE id = {pupilId}",
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Seeds the six-pupil fixture TASK-0061's ordering tests are proven against, in the exact order
    /// spec 6.5.15's widened default sort must produce:
    /// <list type="number">
    /// <item>Primary 1, arm A, surname Z — arm A must place it first in this level despite its surname.</item>
    /// <item>Primary 1, arm B, surname A — proves arm still outranks surname (a surname-first sort
    /// would put this row first instead).</item>
    /// <item>Primary 1, arm Z, surname B — same level, third and last arm.</item>
    /// <item>Primary 2, arm A, surname Y — a HIGHER level ordinal must place it after every Primary 1
    /// row above regardless of its own arm or surname.</item>
    /// <item>The pending→withdrawn lapsed application of 6.5.14 (never held an enrolment).</item>
    /// <item>Transferred (HAD an open enrolment, now closed).</item>
    /// </list>
    /// Rows 5 and 6 are both unenrolled and must sort LAST as one flat block ordered surname then id
    /// — never sub-grouped by status — per the human ruling (<c>decisions/2026-Q3.md</c> 2026-09-15,
    /// TASK-0061). Every surname carries <paramref name="marker"/> purely for readability in a
    /// failing assertion; isolation comes from <see cref="ApiTestFixture.ResetDatabaseAsync"/>
    /// truncating the table before every test, not from the marker.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> SeedRegisterOrderingFixtureAsync(string marker)
    {
        var sessionId = await SeedSessionAsync();
        var primary1Id = await GetClassLevelIdByNameAsync("Primary 1");
        var primary2Id = await GetClassLevelIdByNameAsync("Primary 2");

        var primary1ArmA = await SeedArmAsync(sessionId, "A", primary1Id);
        var primary1ArmB = await SeedArmAsync(sessionId, "B", primary1Id);
        var primary1ArmZ = await SeedArmAsync(sessionId, "Z", primary1Id);
        var primary2ArmA = await SeedArmAsync(sessionId, "A", primary2Id);

        // Surnames are letters-only (Pupil's NamePattern) — no digit suffixes. Each is chosen so that
        // sorting by surname ALONE would give a different, wrong order from the expected one below,
        // except for the two unenrolled rows (5, 6) where surname is the actual tie-break.
        var p1 = await SeedPupilDirectlyAsync($"{marker}Zclass", "One");
        await SetRegistrationNumberAndStatusAsync(p1, $"GRAS/2026/{marker}A", PupilStatus.Active);
        await SeedEnrolmentAsync(p1, primary1ArmA, new DateOnly(2026, 9, 1));

        var p2 = await SeedPupilDirectlyAsync($"{marker}Aclass", "Two");
        await SetRegistrationNumberAndStatusAsync(p2, $"GRAS/2026/{marker}B", PupilStatus.Active);
        await SeedEnrolmentAsync(p2, primary1ArmB, new DateOnly(2026, 9, 1));

        var p3 = await SeedPupilDirectlyAsync($"{marker}Bclass", "Three");
        await SetRegistrationNumberAndStatusAsync(p3, $"GRAS/2026/{marker}C", PupilStatus.Active);
        await SeedEnrolmentAsync(p3, primary1ArmZ, new DateOnly(2026, 9, 1));

        var p4 = await SeedPupilDirectlyAsync($"{marker}Yclass", "Four");
        await SetRegistrationNumberAndStatusAsync(p4, $"GRAS/2026/{marker}D", PupilStatus.Active);
        await SeedEnrolmentAsync(p4, primary2ArmA, new DateOnly(2026, 9, 1));

        var p5 = await SeedPupilDirectlyAsync($"{marker}Uone", "Five");
        await SetStatusAsync(p5, PupilStatus.Withdrawn);

        var p6 = await SeedPupilDirectlyAsync($"{marker}Utwo", "Six");
        var p6EnrolmentId = await SeedEnrolmentAsync(p6, primary1ArmA, new DateOnly(2026, 9, 1));
        await CloseEnrolmentAsync(p6EnrolmentId, new DateOnly(2026, 9, 10));
        await SetRegistrationNumberAndStatusAsync(p6, $"GRAS/2026/{marker}E", PupilStatus.Transferred);

        return [p1, p2, p3, p4, p5, p6];
    }

    /// <summary>
    /// Seeds a regular admin account, a role carrying exactly <paramref name="privileges"/>, and an
    /// ACTIVE <c>role_assignment</c> row — seeded directly through the DbContext (the same accepted
    /// technique <c>AssignmentEndpointsTests</c> uses), so the caller's grants are resolved by the
    /// REAL <c>RoleAssignmentEffectivePrivilegeProvider</c> from genuine database rows, never a fake.
    /// Signs in as that account and returns its cookie jar.
    /// </summary>
    private Task<CookieJar> SignInWithGrantAsync(
        IReadOnlyCollection<string> privileges,
        ScopeType scopeType,
        Guid? sessionId = null,
        IReadOnlyCollection<Guid>? armIds = null) =>
        SignInWithGrantAsync(Client, privileges, scopeType, sessionId, armIds);

    private async Task<CookieJar> SignInWithGrantAsync(
        HttpClient client,
        IReadOnlyCollection<string> privileges,
        ScopeType scopeType,
        Guid? sessionId = null,
        IReadOnlyCollection<Guid>? armIds = null)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync($"Role-{Guid.NewGuid():N}", privileges);
        var resolvedSessionId = sessionId ?? await SeedSessionAsync();

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var creation = Domain.Security.RoleAssignment.Create(
                Guid.CreateVersion7(), accountId, roleId, resolvedSessionId, scopeType, armIds ?? [], accountId);
            creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

            context.Add(creation.Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
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

    private Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null) =>
        PostAsync(Client, url, jar, payload, idempotencyKey);

    private static async Task<HttpResponseMessage> PostAsync<T>(
        HttpClient client, string url, CookieJar jar, T payload, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private Task<HttpResponseMessage> PatchAsync<T>(string url, CookieJar jar, T payload) =>
        PatchAsync(Client, url, jar, payload);

    private static async Task<HttpResponseMessage> PatchAsync<T>(HttpClient client, string url, CookieJar jar, T payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(payload),
        };

        jar.ApplyWithCsrf(request);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
