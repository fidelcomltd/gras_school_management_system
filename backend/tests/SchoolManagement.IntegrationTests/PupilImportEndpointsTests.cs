using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Pupils.Import;
using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Bulk import (spec 6.5.13): the template, the validation pass that writes nothing, and the all-or-nothing commit that
/// issues numbers in file order through the same counter as admission approval. Also the incomplete-records report
/// (spec 6.5.12), which exists to chase what an import leaves missing, so its pupils are made by importing them.
/// </summary>
public sealed class PupilImportEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string TemplateUrl = "/api/v1/pupils/import/template";
    private const string ValidateUrl = "/api/v1/pupils/import/validate";
    private const string CommitUrl = "/api/v1/pupils/import/commit";
    private const string IncompleteUrl = "/api/v1/reports/incomplete-records";

    private static readonly string[] Headers =
    [
        "Surname", "First Name", "Sex", "Date of Birth", "State of Origin", "LGA", "Home Address", "Admission Date",
        "Class Level", "Arm Label", "Mother Name", "Mother Phone", "Emergency Primary Name", "Emergency Primary Relationship",
        "Emergency Primary Phone", "Has Allergy", "Has Medical Condition", "Medical Condition Details", "Takes Medication",
    ];

    [Fact]
    public async Task Template_HoldsTheHeaderRowAndTheActiveSessionsArms()
    {
        RequireDatabase();
        var (sessionId, _, armName) = await SeedSessionAndArmAsync();
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);

        using var response = await GetAsync(TemplateUrl, jar);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)));
        var data = workbook.Worksheet("Pupils");
        data.Cell(1, 1).GetString().ShouldBe("Surname");
        data.Row(1).CellsUsed().Count().ShouldBe(43);
        var accepted = workbook.Worksheet("Accepted values");
        var armColumn = accepted.Row(1).CellsUsed().Single(cell => cell.GetString() == "Arm").Address.ColumnNumber;
        accepted.Column(armColumn).CellsUsed().Select(cell => cell.GetString()).ShouldContain(armName);
        workbook.Worksheet("LGAs").Cell(2, 1).GetString().ShouldBe("Abia");
    }

    [Fact]
    public async Task Validate_ReportsEachRejectionWithRowAndColumn_AndWritesNothing()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync();
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var file = Workbook(
            Row("Okafor", "Chidera", levelName, "A"),
            Row("Bello", "Amina", levelName, "A", dateOfBirth: "03/05/20"),
            Row("Okafor", "Chidera", levelName, "A"),
            Row("Eze", "Ifeanyi", levelName, "Z"));

        using var response = await PostFileAsync(ValidateUrl, jar, file);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await ReadAsync<PupilImportReportDto>(response);
        report.TotalRows.ShouldBe(4);
        report.AcceptedCount.ShouldBe(1);
        report.RejectedCount.ShouldBe(3);
        report.Rows[1].Errors.ShouldContain(issue => issue.Column == "Date of Birth" && issue.Message.Contains("year in full"));
        report.Rows[2].SheetRow.ShouldBe(4);
        report.Rows[2].Errors.ShouldContain(issue => issue.Message.StartsWith("Row 2 ", StringComparison.Ordinal));
        report.Rows[3].Errors.ShouldContain(issue => issue.Column == "Arm Label" && issue.Message.Contains("has no arm named Z"));
        (await CountPupilsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Validate_AFileMissingARequiredColumn_IsRefusedWhole()
    {
        RequireDatabase();
        var (sessionId, _, _) = await SeedSessionAndArmAsync();
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Pupils").Cell(1, 1).Value = "First Name";
        workbook.Worksheet("Pupils").Cell(2, 1).Value = "Chidera";

        using var response = await PostFileAsync(ValidateUrl, jar, Save(workbook));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("import.missing_columns");
    }

    // The counter is the SAME row admission approval increments: seeded at 40, the file takes 41 and 42, in file order.
    [Fact]
    public async Task Commit_ImportsActivePupils_NumberedInFileOrder_WithEnrolmentsAndHealthLeftUnanswered()
    {
        RequireDatabase();
        var (sessionId, levelName, armName) = await SeedSessionAndArmAsync();
        await SeedCounterAsync("2026", 40);
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var bello = Row("Bello", "Amina", armName, null);
        bello["Mother Phone"] = 8031234567d;
        bello["Has Allergy"] = "No";
        bello["Has Medical Condition"] = "Yes";
        bello["Medical Condition Details"] = "Asthma, uses an inhaler";
        bello["Takes Medication"] = "No";
        var file = Workbook(Row("Okafor", "Chidera", levelName, "a"), bello);

        var report = await ValidateAsync(jar, file);
        report.AcceptedCount.ShouldBe(2);
        using var response = await CommitAsync(jar, file, report.FileSha256);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadAsync<PupilImportResultDto>(response);
        result.ImportedCount.ShouldBe(2);
        result.Pupils.Select(pupil => (pupil.SheetRow, pupil.RegistrationNumber)).ShouldBe([(2, "GRAS/2026/0041"), (3, "GRAS/2026/0042")]);

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ids = result.Pupils.Select(pupil => Guid.Parse(pupil.PupilId)).ToList();
        var pupils = await context.Pupils.AsNoTracking().Where(pupil => ids.Contains(pupil.Id)).ToListAsync(TestContext.Current.CancellationToken);
        pupils.ShouldAllBe(pupil => pupil.Status == PupilStatus.Active);
        (await context.Enrolments.AsNoTracking().CountAsync(enrolment => ids.Contains(enrolment.PupilId) && enrolment.EffectiveTo == null, TestContext.Current.CancellationToken))
            .ShouldBe(2);
        var records = await context.Set<AdmissionRecord>().AsNoTracking().Where(record => ids.Contains(record.PupilId)).ToListAsync(TestContext.Current.CancellationToken);
        records.ShouldAllBe(record => !record.DeclarationSigned && record.ApprovedAt != null);

        // Spec 6.5.13: no health columns means no health row (unanswered), never three Nos.
        (await context.Set<PupilHealth>().AsNoTracking().AnyAsync(health => health.PupilId == ids[0], TestContext.Current.CancellationToken)).ShouldBeFalse();
        var health = await context.Set<PupilHealth>().AsNoTracking().SingleAsync(row => row.PupilId == ids[1], TestContext.Current.CancellationToken);
        health.HasAllergy.ShouldBe(false);
        health.HasMedicalCondition.ShouldBe(true);
        var mother = await context.Set<PupilContact>().AsNoTracking()
            .SingleAsync(contact => contact.PupilId == ids[1] && contact.Role == ContactRole.Mother, TestContext.Current.CancellationToken);
        mother.Phone.ShouldBe("+2348031234567");
        mother.IsPrimaryContact.ShouldBeTrue();

        // Spec 6.1.12: one bulk event, a null entity id and a batch id; never a name or a health answer.
        var audit = await context.Set<SchoolManagement.Domain.Audit.AuditEvent>().AsNoTracking()
            .SingleAsync(row => row.Action == Privileges.Pupil.Import, TestContext.Current.CancellationToken);
        audit.EntityId.ShouldBeNull();
        audit.AfterJson.ShouldNotBeNull();
        audit.AfterJson.ShouldContain("batchId");
        audit.AfterJson.ShouldContain("GRAS/2026/0042");
        audit.AfterJson.ShouldNotContain("Bello");
        audit.AfterJson.ShouldNotContain("Asthma");
    }

    // Spec 6.5.10: a number already held (after a manual correction) burns ONE serial, never the whole batch's.
    [Fact]
    public async Task Commit_ANumberAlreadyHeld_BurnsOneSerialOnly()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync();
        await SeedCounterAsync("2026", 40);
        await SeedPupilHoldingNumberAsync("GRAS/2026/0042");
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var file = Workbook(Row("Okafor", "Chidera", levelName, "A"), Row("Bello", "Amina", levelName, "A"), Row("Eze", "Ifeanyi", levelName, "A"));
        var report = await ValidateAsync(jar, file);

        using var response = await CommitAsync(jar, file, report.FileSha256);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilImportResultDto>(response)).Pupils.Select(pupil => pupil.RegistrationNumber)
            .ShouldBe(["GRAS/2026/0041", "GRAS/2026/0043", "GRAS/2026/0044"]);
    }

    // The decisions are part of the request: the same key with different ones is a conflict, not a replay.
    [Fact]
    public async Task Commit_TheSameKeyWithDifferentDecisions_IsAConflict()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync(capacity: 1);
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var file = Workbook(Row("Okafor", "Chidera", levelName, "A"), Row("Bello", "Amina", levelName, "A"));
        var report = await ValidateAsync(jar, file);
        var key = Guid.NewGuid().ToString("D");

        using var first = await CommitAsync(jar, file, report.FileSha256, idempotencyKey: key);
        first.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var changed = await CommitAsync(jar, file, report.FileSha256, overrideCapacity: true, idempotencyKey: key);

        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(changed);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("idempotency.key_conflict");
    }

    [Fact]
    public async Task Commit_WithOneRejectedRow_ImportsNothing()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync();
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var file = Workbook(Row("Okafor", "Chidera", levelName, "A"), Row("Bello", "Amina", levelName, "A", sex: "Boy"));
        var report = await ValidateAsync(jar, file);

        using var response = await CommitAsync(jar, file, report.FileSha256);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("import.rows_rejected");
        (await CountPupilsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Commit_ARegisterMatchNeedsADecision_AndSkipLeavesItOut()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync();
        await SeedPendingPupilAsync("Okafor", "Chidera");
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var file = Workbook(Row("OKAFOR", "chidera", levelName, "A"), Row("Bello", "Amina", levelName, "A"));
        var report = await ValidateAsync(jar, file);
        report.RegisterMatchCount.ShouldBe(1);
        report.Rows[0].RegisterMatches.ShouldHaveSingleItem().Status.ShouldBe(PupilStatus.Pending);

        using var undecided = await CommitAsync(jar, file, report.FileSha256);
        undecided.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using (var document = await ReadJsonAsync(undecided))
        {
            document.RootElement.GetProperty("errorCode").GetString().ShouldBe("import.decision_missing");
        }

        using var skipped = await CommitAsync(jar, file, report.FileSha256, skipRows: [2]);

        skipped.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await ReadAsync<PupilImportResultDto>(skipped);
        result.ImportedCount.ShouldBe(1);
        result.SkippedCount.ShouldBe(1);
        result.Pupils.ShouldHaveSingleItem().SheetRow.ShouldBe(3);
    }

    [Fact]
    public async Task Commit_OverCapacity_NeedsConfirmationAndTheOverridePrivilege()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync(capacity: 1);
        var file = Workbook(Row("Okafor", "Chidera", levelName, "A"), Row("Bello", "Amina", levelName, "A"));
        var importer = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var report = await ValidateAsync(importer, file);
        report.CapacityWarnings.ShouldHaveSingleItem().ImportCount.ShouldBe(2);

        using var unconfirmed = await CommitAsync(importer, file, report.FileSha256);
        unconfirmed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var withoutPrivilege = await CommitAsync(importer, file, report.FileSha256, overrideCapacity: true);
        withoutPrivilege.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var overrider = await SignInWithAsync([Privileges.Pupil.Import, Privileges.Arm.CapacityOverride], sessionId);
        using var overridden = await CommitAsync(overrider, file, report.FileSha256, overrideCapacity: true);

        overridden.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync<PupilImportResultDto>(overridden)).ImportedCount.ShouldBe(2);
    }

    [Fact]
    public async Task Commit_WithADifferentFilesHash_IsRefused()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync();
        var jar = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        var file = Workbook(Row("Okafor", "Chidera", levelName, "A"));

        using var response = await CommitAsync(jar, file, new string('a', 64));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var document = await ReadJsonAsync(response);
        document.RootElement.GetProperty("errorCode").GetString().ShouldBe("import.file_changed");
        (await CountPupilsAsync()).ShouldBe(0);
    }

    // Imported pupils are active with the declaration unrecorded and the barred question unasked; one also has no
    // emergency contact and no health answers. The report names each gap per pupil and counts it.
    [Fact]
    public async Task IncompleteRecords_ListsWhatImportedPupilsStillLack()
    {
        RequireDatabase();
        var (sessionId, levelName, armName) = await SeedSessionAndArmAsync();
        var importer = await SignInWithAsync([Privileges.Pupil.Import, Privileges.Report.View], sessionId);
        var okafor = Row("Okafor", "Chidera", levelName, "A");
        okafor.Remove("Emergency Primary Name");
        okafor.Remove("Emergency Primary Relationship");
        okafor.Remove("Emergency Primary Phone");
        var bello = Row("Bello", "Amina", levelName, "A");
        bello["Has Allergy"] = "No";
        bello["Has Medical Condition"] = "No";
        bello["Takes Medication"] = "No";
        await ImportAsync(importer, Workbook(okafor, bello));

        using var response = await GetAsync(IncompleteUrl, importer);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await ReadAsync<SchoolManagement.Application.Pupils.Records.IncompleteRecordsReportDto>(response);
        report.SessionName.ShouldBe("2026/2027");
        report.PupilsChecked.ShouldBe(2);
        report.Pupils.Select(pupil => pupil.Surname).ShouldBe(["Bello", "Okafor"]);
        report.Pupils.ShouldAllBe(pupil => pupil.ArmName == armName);
        int Count(string code) => report.Counts.SingleOrDefault(count => count.Code == code)?.Count ?? 0;
        Count("declaration.unsigned").ShouldBe(2);
        Count("contacts.emergency_primary").ShouldBe(1);
        Count("health.unanswered").ShouldBe(1);
        report.Counts.Single(count => count.Code == "contacts.emergency_primary").Required.ShouldBeTrue();
        report.Pupils.Single(pupil => pupil.Surname == "Okafor").Required.Select(item => item.Code)
            .ShouldContain("contacts.emergency_primary");
        report.Pupils.Single(pupil => pupil.Surname == "Bello").Required.Select(item => item.Code)
            .ShouldNotContain("health.unanswered");
    }

    [Fact]
    public async Task IncompleteRecords_AnArmRestrictedGrantSeesOnlyItsArms()
    {
        RequireDatabase();
        var (sessionId, levelName, _) = await SeedSessionAndArmAsync();
        var otherArmId = await SeedArmAsync(sessionId, "B");
        var importer = await SignInWithAsync([Privileges.Pupil.Import], sessionId);
        await ImportAsync(importer, Workbook(Row("Okafor", "Chidera", levelName, "A"), Row("Bello", "Amina", levelName, "B")));
        var teacher = await SignInWithAsync([Privileges.Report.View], sessionId, armIds: [otherArmId]);

        using var response = await GetAsync(IncompleteUrl, teacher);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await ReadAsync<SchoolManagement.Application.Pupils.Records.IncompleteRecordsReportDto>(response);
        report.PupilsChecked.ShouldBe(1);
        report.Pupils.ShouldHaveSingleItem().Surname.ShouldBe("Bello");
        report.Pupils[0].ArmId.ShouldBe(otherArmId.ToString("D"));

        var anyOtherArm = Guid.NewGuid().ToString("D");
        using var outside = await GetAsync($"{IncompleteUrl}?armId={anyOtherArm}", teacher);
        outside.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The route only authenticates; without report.view in any scope the handler refuses.
        using var withoutReportView = await GetAsync(IncompleteUrl, importer);
        withoutReportView.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Files -----------------------------------------------------------------------------------

    private static Dictionary<string, object> Row(
        string surname, string firstName, string classLevel, string? armLabel, string dateOfBirth = "03/05/2020", string sex = "Female")
    {
        var row = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Surname"] = surname,
            ["First Name"] = firstName,
            ["Sex"] = sex,
            ["Date of Birth"] = dateOfBirth,
            ["State of Origin"] = "Anambra",
            ["LGA"] = "Awka South",
            ["Home Address"] = "14 Zik Avenue, Awka",
            ["Admission Date"] = "05/01/2026",
            ["Class Level"] = classLevel,
            ["Mother Name"] = "Ngozi " + surname,
            ["Mother Phone"] = "08031234567",
            ["Emergency Primary Name"] = "Obi " + surname,
            ["Emergency Primary Relationship"] = "Uncle",
            ["Emergency Primary Phone"] = "08059876543",
        };
        if (armLabel is not null)
        {
            row["Arm Label"] = armLabel;
        }

        return row;
    }

    private static byte[] Workbook(params Dictionary<string, object>[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Pupils");
        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = Headers[column];
        }

        for (var row = 0; row < rows.Length; row++)
        {
            foreach (var (header, value) in rows[row])
            {
                var cell = sheet.Cell(row + 2, Array.IndexOf(Headers, header) + 1);
                cell.Value = value is double number ? number : (string)value;
            }
        }

        return Save(workbook);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ---- Seeding ---------------------------------------------------------------------------------

    private async Task<(Guid SessionId, string LevelName, string ArmName)> SeedSessionAndArmAsync(int? capacity = null)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 20)).Value;
        session.Activate();
        var level = await context.ClassLevels.AsNoTracking().OrderBy(candidate => candidate.ProgressionOrder).FirstAsync(TestContext.Current.CancellationToken);
        var arm = Arm.Create(Guid.CreateVersion7(), level.Id, session.Id, "A", capacity, null).Value;
        context.AddRange(session, arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (session.Id, level.Name, ArmDisplayName.Compose(level.Name, "A"));
    }

    private async Task SeedPendingPupilAsync(string surname, string firstName)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, firstName, middleName: null, PupilSex.Female, new DateOnly(2020, 5, 3),
            asOfDate: new DateOnly(2026, 9, 1), nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
            previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.Add(pupil);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedPupilHoldingNumberAsync(string registrationNumber)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Holder", "Ofanumber", middleName: null, PupilSex.Male, new DateOnly(2019, 1, 1),
            asOfDate: new DateOnly(2026, 9, 1), nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka",
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

    private async Task<int> CountPupilsAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Pupils.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedArmAsync(Guid sessionId, string label)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var level = await context.ClassLevels.AsNoTracking().OrderBy(candidate => candidate.ProgressionOrder).FirstAsync(TestContext.Current.CancellationToken);
        var arm = Arm.Create(Guid.CreateVersion7(), level.Id, sessionId, label, null, null).Value;
        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return arm.Id;
    }

    private async Task ImportAsync(CookieJar jar, byte[] file)
    {
        var report = await ValidateAsync(jar, file);
        report.RejectedCount.ShouldBe(0);
        using var response = await CommitAsync(jar, file, report.FileSha256);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<CookieJar> SignInWithAsync(IReadOnlyCollection<string> privileges, Guid sessionId, Guid[]? armIds = null)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var role = Role.Create(Guid.CreateVersion7(), $"Role-{Guid.NewGuid():N}", null, privileges).Value;
            var assignment = RoleAssignment.Create(
                Guid.CreateVersion7(), accountId, role.Id, sessionId, armIds is null ? ScopeType.SchoolWide : ScopeType.ArmList, armIds ?? [],
                accountId).Value;
            context.AddRange(role, assignment);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        using (await GetAsync("/api/v1/auth/csrf", jar))
        {
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(request);
        using var signIn = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        jar.Capture(signIn);
        return jar;
    }

    // ---- HTTP ------------------------------------------------------------------------------------

    private async Task<PupilImportReportDto> ValidateAsync(CookieJar jar, byte[] file)
    {
        using var response = await PostFileAsync(ValidateUrl, jar, file);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadAsync<PupilImportReportDto>(response);
    }

    private Task<HttpResponseMessage> CommitAsync(
        CookieJar jar, byte[] file, string fileSha256, int[]? skipRows = null, bool overrideCapacity = false, string? idempotencyKey = null)
    {
        var fields = new List<(string Name, string Value)> { ("fileSha256", fileSha256) };
        fields.AddRange((skipRows ?? []).Select(row => ("skipRows", row.ToString(CultureInfo.InvariantCulture))));
        if (overrideCapacity)
        {
            fields.Add(("overrideCapacity", "true"));
        }

        return PostFileAsync(CommitUrl, jar, file, fields, idempotencyKey: idempotencyKey ?? Guid.NewGuid().ToString("D"));
    }

    private async Task<HttpResponseMessage> PostFileAsync(
        string url, CookieJar jar, byte[] file, IReadOnlyList<(string Name, string Value)>? fields = null, string? idempotencyKey = null)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", "register.xlsx" } };
        foreach (var (name, value) in fields ?? [])
        {
            form.Add(new StringContent(value), name);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
