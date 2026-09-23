using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Admissions;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0051: registration-number issue and the admission-approval transition (spec 6.5.10, 6.5.11
/// step 9, 6.5.14). Every test seeds directly through the DbContext up to the point admission
/// approval itself is under test — the same accepted technique <c>PupilEndpointsTests</c> already
/// uses for state no earlier endpoint can produce.
/// </summary>
public sealed class AdmissionApprovalEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";
    private const string AdmissionsUrl = "/api/v1/admissions";
    private const string SettingsUrl = "/api/v1/settings";

    private static readonly DateOnly DefaultDateOfBirth = new(2020, 5, 3);

    [Fact]
    public async Task Approve_HappyPath_IssuesANumberOpensAnEnrolmentAndSetsActive()
    {
        RequireDatabase();

        var (sessionId, termId, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        _ = termId;
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Approve", "Happypath", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.Status.ShouldBe(PupilStatus.Active);
        body.RegistrationNumber.ShouldNotBeNullOrWhiteSpace();
        body.RegistrationNumber.ShouldStartWith("GRAS/2026/");

        var enrolmentExists = await EnrolmentExistsAsync(pupilId, armId);
        enrolmentExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Approve_TheYearComesFromDateAdmitted_NeverFromToday()
    {
        RequireDatabase();

        // The admission is dated September 2026; the CLOCK is January 2027 when approval happens.
        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Yearcomesfrom", "Admitteddate", sessionId, levelId, new DateOnly(2026, 9, 10));

        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2027, 1, 15, 9, 0, 0, TimeSpan.Zero));
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<TimeProvider>(fakeTime)));
        using var client = factory.CreateClient();

        var jar = await SignInWithGrantAsync(client, [Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(client, pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.RegistrationNumber.ShouldStartWith("GRAS/2026/"); // NOT 2027.
    }

    [Fact]
    public async Task Approve_TheIssuedNumberIsFrozen_ChangingTheAbbreviationAfterwardDoesNotChangeIt()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Frozen", "Abbreviation", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);
        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var issued = (await ReadAsync<PupilDto>(response)).RegistrationNumber!;
        issued.ShouldStartWith("GRAS/");

        await SetAbbreviationDirectlyAsync("CHNG");

        var reread = await GetPupilRegistrationNumberAsync(pupilId);
        reread.ShouldBe(issued);
    }

    // Spec 6.5.10 rule 2/4: the counter is a real row lock, proven with two GENUINELY concurrent
    // approvals racing on the SAME counter partition (both admitted in 2026), never asserted from
    // sequential calls. Both requests are built and fired before either is awaited — the same
    // technique ArmEndpointsTests' own concurrency proof uses.
    [Fact]
    public async Task Approve_TwoConcurrentApprovalsInTheSameYear_ProduceTwoDifferentSerials()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1), capacity: 50);
        var (firstPupilId, _) = await SeedApprovableAdmissionAsync("Concurrentone", "Race", sessionId, levelId, new DateOnly(2026, 9, 10));
        var (secondPupilId, _) = await SeedApprovableAdmissionAsync("Concurrenttwo", "Race", sessionId, levelId, new DateOnly(2026, 9, 11));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        using var request1 = BuildApproveRequest(firstPupilId, jar, armId, $"key-{Guid.NewGuid():N}");
        using var request2 = BuildApproveRequest(secondPupilId, jar, armId, $"key-{Guid.NewGuid():N}");

        var task1 = Client.SendAsync(request1, TestContext.Current.CancellationToken);
        var task2 = Client.SendAsync(request2, TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(task1, task2);

        try
        {
            foreach (var result in results)
            {
                result.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            var numbers = new List<string>();

            foreach (var result in results)
            {
                var body = await ReadAsync<PupilDto>(result);
                numbers.Add(body.RegistrationNumber!);
            }

            numbers.Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        }
        finally
        {
            foreach (var result in results)
            {
                result.Dispose();
            }
        }
    }

    [Fact]
    public async Task Approve_IdempotencyKeyReplay_ReturnsTheFirstResponseAndAdvancesTheCounterExactlyOnce()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Idempotent", "Replay", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);
        var key = $"key-{Guid.NewGuid():N}";

        var serialBefore = await GetLastSerialAsync("2026");

        var first = await ApproveAsync(pupilId, jar, armId, key);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await ReadAsync<PupilDto>(first);

        var second = await ApproveAsync(pupilId, jar, armId, key);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.TryGetValues("Idempotency-Replay", out var replayHeader).ShouldBeTrue();
        replayHeader!.ShouldContain("true");
        var secondBody = await ReadAsync<PupilDto>(second);

        secondBody.RegistrationNumber.ShouldBe(firstBody.RegistrationNumber);

        var serialAfter = await GetLastSerialAsync("2026");
        (serialAfter - serialBefore).ShouldBe(1);
    }

    // Spec 6.5.10 rule 4's backstop, proven with a REAL unique-index violation rather than a fake: the
    // counter is pre-set so the FIRST candidate serial composes to a number a DIFFERENT pupil already
    // holds. The retry must succeed on its second attempt with the NEXT serial.
    [Fact]
    public async Task Approve_WhenTheFirstCandidateNumberIsAlreadyTaken_RetriesAndSucceedsWithTheNextSerial()
    {
        RequireDatabase();

        // A PAST admission year (2023), deliberately distinct from every other test's 2026 — the
        // record's OWN date_admitted must never be in the future relative to the real clock, since
        // AdmissionRecord.Update re-validates it on every call regardless of which fields change.
        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2023, 9, 1));
        await SeedCounterAsync("2023", lastSerial: 0);
        await SeedPupilHoldingRegistrationNumberAsync("GRAS/2023/0001");
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Retrysucceeds", "Onsecondattempt", sessionId, levelId, new DateOnly(2023, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.RegistrationNumber.ShouldBe("GRAS/2023/0002");
    }

    // Exhausts all THREE attempts: serials 1, 2 and 3 are all already taken, so the fourth (never
    // attempted — MaxIssueAttempts is 3) would be the first free one. Proves the verbatim failure
    // message, that the retry loop does not spin forever, AND acceptance criterion 8's rollback:
    // three internal attempts each ran a REAL counter increment and staged a pupil/enrolment write
    // before failing, yet the pupil is still pending, holds no number, no enrolment exists, and the
    // counter is back at its PRE-attempt value — proof that a failing command rolls back the WHOLE
    // ambient transaction, not just the failed SaveChanges savepoint, so nothing from any attempt
    // survives once the command as a whole fails.
    [Fact]
    public async Task Approve_WhenThreeConsecutiveCandidateNumbersAreTaken_FailsAndRollsBackNothingHalfWritten()
    {
        RequireDatabase();

        // A PAST admission year (2024), distinct from every other test — see the retry-succeeds
        // test's own remark on why this can never be a future date.
        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2024, 9, 1));
        await SeedCounterAsync("2024", lastSerial: 0);
        await SeedPupilHoldingRegistrationNumberAsync("GRAS/2024/0001");
        await SeedPupilHoldingRegistrationNumberAsync("GRAS/2024/0002");
        await SeedPupilHoldingRegistrationNumberAsync("GRAS/2024/0003");
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Retryexhausted", "Allthreetaken", sessionId, levelId, new DateOnly(2024, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("pupil.registration_number_issue_failed");
        document.RootElement.GetProperty("detail").GetString().ShouldBe("Could not issue a registration number. Try again.");

        // Rollback proof: nothing from any of the three failed attempts survived.
        var (status, registrationNumber) = await GetPupilStatusAndRegistrationNumberAsync(pupilId);
        status.ShouldBe(PupilStatus.Pending);
        registrationNumber.ShouldBeNull();
        (await EnrolmentExistsAsync(pupilId, armId)).ShouldBeFalse();
        (await GetLastSerialAsync("2024")).ShouldBe(0); // Back to its PRE-attempt seeded value, not 3.
    }

    [Fact]
    public async Task Approve_NoActiveTerm_Returns409WithTheVerbatimMessage()
    {
        RequireDatabase();

        var sessionId = await SeedActiveSessionAsync(new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 25));
        var levelId = await GetFirstClassLevelIdAsync();
        var armId = await SeedArmAsync(levelId, sessionId, "Noterm");
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Noactive", "Term", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admission.no_active_term");
        document.RootElement.GetProperty("detail").GetString()
            .ShouldBe("No term is currently active. Open a term before approving admissions.");
    }

    [Fact]
    public async Task Approve_ArmBelongsToADifferentLevel_Returns422()
    {
        RequireDatabase();

        var (sessionId, _, levelId, _) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var otherLevelId = await GetSecondClassLevelIdAsync();
        var wrongArmId = await SeedArmAsync(otherLevelId, sessionId, "Wronglevel");
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Wrong", "Level", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, wrongArmId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admission.arm_not_available");
    }

    [Fact]
    public async Task Approve_ARequiredAssessmentWithNoRecordedOutcome_Returns422()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync(
            "Assessment", "Missing", sessionId, levelId, new DateOnly(2026, 9, 10), assessmentRequired: true);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString()
            .ShouldBe("admission_record.assessment_result_remarks_required_for_approval");
    }

    [Fact]
    public async Task Approve_ARequiredAssessmentWithASuppliedOutcome_Succeeds()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync(
            "Assessment", "Supplied", sessionId, levelId, new DateOnly(2026, 9, 10), assessmentRequired: true);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(
            pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}", assessmentResultRemarks: "Passed the entrance assessment.");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Approve_TheDeclarationIsUnsigned_Returns422()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync(
            "Declaration", "Unsigned", sessionId, levelId, new DateOnly(2026, 9, 10), declarationSigned: false);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admission.declaration_not_signed");
    }

    [Fact]
    public async Task Approve_ContactsHealthAndTheBarredQuestionMissing_Returns422NamingEachStep()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync(
            "Sections", "Missing", sessionId, levelId, new DateOnly(2026, 9, 10), sectionsComplete: false);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admission.incomplete");
        var detail = document.RootElement.GetProperty("detail").GetString() ?? string.Empty;
        detail.ShouldContain("Step 3");
        detail.ShouldContain("Step 4");
        detail.ShouldContain("Step 5");
    }

    [Fact]
    public async Task Approve_HeadOfSchoolConfirmedFalse_Returns422()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Headofschool", "Notconfirmed", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}", headOfSchoolConfirmed: false);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Approve_HeadOfSchoolNameOmitted_DefaultsFromSettings_SuppliedValueOverrides()
    {
        RequireDatabase();

        await SetHeadTeacherNameDirectlyAsync("Mrs. Adaeze Nwosu");

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (omittedPupilId, _) = await SeedApprovableAdmissionAsync("Omitted", "Default", sessionId, levelId, new DateOnly(2026, 9, 10));
        var (suppliedPupilId, _) = await SeedApprovableAdmissionAsync("Supplied", "Override", sessionId, levelId, new DateOnly(2026, 9, 11));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var omittedResponse = await ApproveAsync(omittedPupilId, jar, armId, key: $"key-{Guid.NewGuid():N}", headOfSchoolName: null);
        omittedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var omittedBody = await ReadAsync<PupilDto>(omittedResponse);
        omittedBody.Admission!.HeadOfSchoolName.ShouldBe("Mrs. Adaeze Nwosu");

        var suppliedResponse = await ApproveAsync(
            suppliedPupilId, jar, armId, key: $"key-{Guid.NewGuid():N}", headOfSchoolName: "Mr. Obinna Eze");
        suppliedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var suppliedBody = await ReadAsync<PupilDto>(suppliedResponse);
        suppliedBody.Admission!.HeadOfSchoolName.ShouldBe("Mr. Obinna Eze");
    }

    [Fact]
    public async Task Approve_AnAlreadyApprovedAdmission_Returns409()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Already", "Approved", sessionId, levelId, new DateOnly(2026, 9, 10));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);
        var first = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await ApproveAsync(pupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(second);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("admission.already_approved");
    }

    [Fact]
    public async Task Approve_ArmAtCapacityWithoutOverride_Returns409()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1), capacity: 1);
        var (fillerPupilId, _) = await SeedApprovableAdmissionAsync(
            "Filler", "TakesTheOnlySpace", sessionId, levelId, new DateOnly(2026, 9, 5));

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);
        var fillerResponse = await ApproveAsync(fillerPupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");
        fillerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (secondPupilId, _) = await SeedApprovableAdmissionAsync("Blocked", "ByCapacity", sessionId, levelId, new DateOnly(2026, 9, 6));

        var response = await ApproveAsync(secondPupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("arm.at_capacity");
    }

    [Fact]
    public async Task Approve_ArmAtCapacityWithOverride_SucceedsAndAudits()
    {
        RequireDatabase();

        var auditSink = new RecordingSystemAuditSink();
        using var factory = Fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemAuditSink>();
            services.AddSingleton<ISystemAuditSink>(auditSink);
        }));
        using var client = factory.CreateClient();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1), capacity: 1);
        var (fillerPupilId, _) = await SeedApprovableAdmissionAsync("Filler", "TakesTheSpace", sessionId, levelId, new DateOnly(2026, 9, 5));

        var jar = await SignInWithGrantAsync(client, [Privileges.Pupil.AdmissionApprove], sessionId);
        var fillerResponse = await ApproveAsync(client, fillerPupilId, jar, armId, key: $"key-{Guid.NewGuid():N}");
        fillerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var overrideJar = await SignInWithGrantAsync(
            client, [Privileges.Pupil.AdmissionApprove, Privileges.Arm.CapacityOverride], sessionId);
        var (secondPupilId, _) = await SeedApprovableAdmissionAsync("Overrides", "Capacity", sessionId, levelId, new DateOnly(2026, 9, 6));

        var response = await ApproveAsync(client, secondPupilId, overrideJar, armId, key: $"key-{Guid.NewGuid():N}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        auditSink.Records.ShouldContain(record =>
            record.Action == Privileges.Arm.CapacityOverride && record.EntityId == armId.ToString("D", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Decline_HappyPath_SetsWithdrawnIssuesNoNumberAndLeavesTheCounterUntouched()
    {
        RequireDatabase();

        var (sessionId, _, levelId, _) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Decline", "Happypath", sessionId, levelId, new DateOnly(2026, 9, 10));
        await SeedCounterAsync("2026", lastSerial: 5);

        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await DeclineAsync(pupilId, jar, "Family relocated before the intake began.");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync<PupilDto>(response);
        body.Status.ShouldBe(PupilStatus.Withdrawn);
        body.RegistrationNumber.ShouldBeNull();

        var serialAfter = await GetLastSerialAsync("2026");
        serialAfter.ShouldBe(5); // Asserted directly against the counter row, per the card's own instruction.
    }

    [Fact]
    public async Task Decline_UnknownId_Returns404()
    {
        RequireDatabase();

        var sessionId = await SeedActiveSessionAsync(new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 25));
        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await DeclineAsync(Guid.CreateVersion7(), jar, "No such pupil.");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Decline_WithNoReason_Returns422()
    {
        RequireDatabase();

        var (sessionId, _, levelId, _) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (pupilId, _) = await SeedApprovableAdmissionAsync("Decline", "Noreason", sessionId, levelId, new DateOnly(2026, 9, 10));
        var jar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);

        var response = await DeclineAsync(pupilId, jar, reason: string.Empty);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // TASK-0051's own named trigger for the standing drift: this card wires the real count.
    [Fact]
    public async Task Settings_IssuedCountReflectsApprovedAdmissionsOnly_NotDeclinedOnes()
    {
        RequireDatabase();

        var (sessionId, _, levelId, armId) = await SeedActiveSessionTermAndArmAsync(new DateOnly(2026, 9, 1));
        var (approvedPupilId, _) = await SeedApprovableAdmissionAsync("Counted", "Approved", sessionId, levelId, new DateOnly(2026, 9, 10));
        var (declinedPupilId, _) = await SeedApprovableAdmissionAsync("Notcounted", "Declined", sessionId, levelId, new DateOnly(2026, 9, 10));

        var actionJar = await SignInWithGrantAsync([Privileges.Pupil.AdmissionApprove], sessionId);
        (await ApproveAsync(approvedPupilId, actionJar, armId, key: $"key-{Guid.NewGuid():N}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DeclineAsync(declinedPupilId, actionJar, "Changed their mind.")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var readJar = await SignInWithGrantAsync([Privileges.Settings.View], sessionId);
        var response = await GetAsync(SettingsUrl, readJar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await ReadAsync<SettingsDto>(response);
        settings.Abbreviation.IssuedCount.ShouldBe(1);
    }

    // ---- Seeding helpers -----------------------------------------------------------------------

    private async Task<(Guid SessionId, Guid TermId, Guid LevelId, Guid ArmId)> SeedActiveSessionTermAndArmAsync(
        DateOnly sessionStart, int? capacity = null)
    {
        var sessionId = await SeedActiveSessionAsync(sessionStart, sessionStart.AddYears(1).AddDays(-50));
        var termId = await SeedActiveTermAsync(sessionId, sessionStart, sessionStart.AddMonths(3));
        var levelId = await GetFirstClassLevelIdAsync();
        var armId = await SeedArmAsync(levelId, sessionId, $"A{Guid.NewGuid():N}"[..8], capacity);

        return (sessionId, termId, levelId, armId);
    }

    private async Task<Guid> SeedActiveSessionAsync(DateOnly startDate, DateOnly endDate)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var name = $"{startDate.Year}/{startDate.Year + 1}";
        var session = AcademicSession.Create(Guid.CreateVersion7(), name, startDate, endDate).Value;
        session.Activate();

        context.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return session.Id;
    }

    private async Task<Guid> SeedActiveTermAsync(Guid sessionId, DateOnly startDate, DateOnly endDate)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var term = Term.Create(Guid.CreateVersion7(), sessionId, ordinal: 1, "First Term", startDate, endDate).Value;
        term.Open().IsSuccess.ShouldBeTrue();

        context.Add(term);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return term.Id;
    }

    private async Task<Guid> GetFirstClassLevelIdAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return (await context.ClassLevels.AsNoTracking()
            .OrderBy(level => level.ProgressionOrder)
            .FirstAsync(TestContext.Current.CancellationToken)).Id;
    }

    private async Task<Guid> GetSecondClassLevelIdAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return (await context.ClassLevels.AsNoTracking()
            .OrderBy(level => level.ProgressionOrder)
            .Skip(1)
            .FirstAsync(TestContext.Current.CancellationToken)).Id;
    }

    private async Task<Guid> SeedArmAsync(Guid classLevelId, Guid sessionId, string label, int? capacity = null)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var arm = Arm.Create(Guid.CreateVersion7(), classLevelId, sessionId, label, capacity, null).Value;

        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }

    /// <summary>
    /// Seeds a pending pupil plus an admission record that satisfies EVERY blocking condition except
    /// the ones a test deliberately withholds — ready to approve as-is by default.
    /// </summary>
    private async Task<(Guid PupilId, Guid AdmissionRecordId)> SeedApprovableAdmissionAsync(
        string surname,
        string firstName,
        Guid sessionId,
        Guid classLevelId,
        DateOnly dateAdmitted,
        bool assessmentRequired = false,
        bool declarationSigned = true,
        bool sectionsComplete = true)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, firstName, middleName: null, PupilSex.Female, DefaultDateOfBirth,
            asOfDate: dateAdmitted, nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;

        var record = AdmissionRecord.Create(
            Guid.CreateVersion7(), pupil.Id, sessionId, dateApplicationReceived: null, dateAdmitted,
            asOfDate: dateAdmitted, classLevelId, AdmissionType.New, admissionTypeNote: null, assessmentRequired).Value;

        record.Update(
            null, null, null, null, null, null, null, null, null,
            "Test Parent", declarationSigned, declarationSigned ? dateAdmitted : null, null, null, dateAdmitted)
            .IsSuccess.ShouldBeTrue();

        context.Add(pupil);
        context.Add(record);
        if (sectionsComplete)
        {
            // Spec 6.5.12's other blocking items: a responsible adult as primary contact, the primary emergency
            // contact, the barred-persons answer and the three health answers.
            var father = PupilContact.Create(Guid.CreateVersion7(), pupil.Id, ContactRole.Father);
            father.Apply("Test Parent", null, "08031234567", null, null, null, isPrimary: true).IsSuccess.ShouldBeTrue();
            var emergency = PupilContact.Create(Guid.CreateVersion7(), pupil.Id, ContactRole.EmergencyPrimary);
            emergency.Apply("Test Relative", "Aunt", "08059876543", null, null, null, isPrimary: false).IsSuccess.ShouldBeTrue();
            var health = PupilHealth.Create(pupil.Id);
            health.Apply(false, null, false, null, false, null, null, null, null, null, null).IsSuccess.ShouldBeTrue();
            context.AddRange(father, emergency, health, BarredPersonAnswer.Create(pupil.Id, hasBarredPersons: false));
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (pupil.Id, record.Id);
    }

    private async Task SeedPupilHoldingRegistrationNumberAsync(string registrationNumber)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Holder", "OfANumber", middleName: null, PupilSex.Female, DefaultDateOfBirth,
            asOfDate: new DateOnly(2026, 1, 1), nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;

        context.Add(pupil);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET registration_number = {registrationNumber}, status = 'Active' WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);
    }

    private async Task SeedCounterAsync(string counterKey, int lastSerial)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO registration_counter (counter_key, last_serial) VALUES ({counterKey}, {lastSerial})
            ON CONFLICT (counter_key) DO UPDATE SET last_serial = {lastSerial}
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task<int> GetLastSerialAsync(string counterKey)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.RegistrationCounters.AsNoTracking()
            .Where(counter => counter.Id == counterKey)
            .Select(counter => (int?)counter.LastSerial)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken) ?? 0;
    }

    private async Task<string?> GetPupilRegistrationNumberAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Pupils.IgnoreQueryFilters().AsNoTracking()
            .Where(pupil => pupil.Id == pupilId)
            .Select(pupil => pupil.RegistrationNumber)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(PupilStatus Status, string? RegistrationNumber)> GetPupilStatusAndRegistrationNumberAsync(Guid pupilId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Pupils.IgnoreQueryFilters().AsNoTracking()
            .Where(pupil => pupil.Id == pupilId)
            .Select(pupil => new ValueTuple<PupilStatus, string?>(pupil.Status, pupil.RegistrationNumber))
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> EnrolmentExistsAsync(Guid pupilId, Guid armId)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Enrolments.AsNoTracking()
            .AnyAsync(enrolment => enrolment.PupilId == pupilId && enrolment.ArmId == armId && enrolment.EffectiveTo == null,
                TestContext.Current.CancellationToken);
    }

    private async Task SetAbbreviationDirectlyAsync(string abbreviation)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE school_profile SET abbreviation = {abbreviation} WHERE id = {SchoolProfile.SingletonId}",
            TestContext.Current.CancellationToken);
    }

    private async Task SetHeadTeacherNameDirectlyAsync(string headTeacherName)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE school_profile SET head_teacher_name = {headTeacherName} WHERE id = {SchoolProfile.SingletonId}",
            TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedRoleAsync(string name, IReadOnlyCollection<string> privileges)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var role = Role.Create(Guid.CreateVersion7(), name, null, privileges).Value;

        context.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    private Task<CookieJar> SignInWithGrantAsync(IReadOnlyCollection<string> privileges, Guid sessionId) =>
        SignInWithGrantAsync(Client, privileges, sessionId);

    private async Task<CookieJar> SignInWithGrantAsync(HttpClient client, IReadOnlyCollection<string> privileges, Guid sessionId)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);
        var roleId = await SeedRoleAsync($"Role-{Guid.NewGuid():N}", privileges);

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var assignment = RoleAssignment.Create(
                Guid.CreateVersion7(), accountId, roleId, sessionId, ScopeType.SchoolWide, [], accountId).Value;

            context.Add(assignment);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        await GetAsync(client, CsrfUrl, jar);
        var signIn = await PostAsync(client, SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password));
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    // ---- HTTP helpers ----------------------------------------------------------------------------

    private Task<HttpResponseMessage> ApproveAsync(
        Guid pupilId,
        CookieJar jar,
        Guid armId,
        string key,
        string? assessmentResultRemarks = null,
        bool headOfSchoolConfirmed = true,
        string? headOfSchoolName = null) =>
        ApproveAsync(Client, pupilId, jar, armId, key, assessmentResultRemarks, headOfSchoolConfirmed, headOfSchoolName);

    private static Task<HttpResponseMessage> ApproveAsync(
        HttpClient client,
        Guid pupilId,
        CookieJar jar,
        Guid armId,
        string key,
        string? assessmentResultRemarks = null,
        bool headOfSchoolConfirmed = true,
        string? headOfSchoolName = null)
    {
        var command = new ApproveAdmissionCommand(
            Guid.Empty,
            armId.ToString("D", CultureInfo.InvariantCulture),
            assessmentResultRemarks,
            headOfSchoolConfirmed,
            headOfSchoolName);

        return PostAsync(client, $"{AdmissionsUrl}/{pupilId}/approve", jar, command, key);
    }

    private static HttpRequestMessage BuildApproveRequest(Guid pupilId, CookieJar jar, Guid armId, string key)
    {
        var command = new ApproveAdmissionCommand(
            Guid.Empty, armId.ToString("D", CultureInfo.InvariantCulture), null, true, null);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{AdmissionsUrl}/{pupilId}/approve")
        {
            Content = JsonContent.Create(command),
        };
        request.Headers.Add("Idempotency-Key", key);
        jar.ApplyWithCsrf(request);

        return request;
    }

    private Task<HttpResponseMessage> DeclineAsync(Guid pupilId, CookieJar jar, string reason)
    {
        var command = new DeclineAdmissionCommand(Guid.Empty, reason);

        return PostAsync(Client, $"{AdmissionsUrl}/{pupilId}/decline", jar, command, idempotencyKey: null);
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
}
