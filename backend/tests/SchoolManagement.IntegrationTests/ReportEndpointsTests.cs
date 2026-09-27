using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Reports;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>Spec 15 section 10's results reports over one computed arm, and the audited export.</summary>
public sealed class ReportEndpointsTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static int _nextYear = 9700;
    private static int _nextFeeOrder = 50_000;

    [Fact]
    public async Task ResultsReports_ReadTheComputedArm_AndAnExportIsAudited()
    {
        RequireDatabase();
        var (jar, adminId) = await SignInAdminAsync();
        var seeded = await SeedAsync();
        var query = $"termId={seeded.TermId}";

        var broadsheet = await GetReportAsync(jar, $"/api/v1/reports/broadsheet?{query}&armId={seeded.ArmId}");
        broadsheet.Rows.Count.ShouldBe(2);
        broadsheet.Rows[0].Cells[1].ShouldBe("EZE Chidera"); // arm position 1 first
        broadsheet.Columns.Count(column => column.Group == seeded.SubjectName).ShouldBe(3);
        broadsheet.Notes.ShouldContain(note => note.StartsWith("Not yet published", StringComparison.Ordinal));

        var merit = await GetReportAsync(jar, $"/api/v1/reports/merit-list?{query}&levelId={seeded.LevelId}&top=1");
        merit.Rows.Select(row => row.Cells[1]).ShouldBe(["EZE Chidera"]);

        var progress = await GetReportAsync(jar, $"/api/v1/reports/result-entry-progress?{query}&state=Approved");
        progress.Rows.ShouldContain(row => row.Cells[0] == seeded.ArmName && row.Cells[8] == "Approved");

        using (var bad = await GetAsync(jar, $"/api/v1/reports/broadsheet/export?{query}&armId={seeded.ArmId}&format=xls"))
        {
            bad.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        using (var csv = await GetAsync(jar, $"/api/v1/reports/broadsheet/export?{query}&armId={seeded.ArmId}&format=csv"))
        {
            csv.StatusCode.ShouldBe(HttpStatusCode.OK);
            csv.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
            (await csv.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("EZE Chidera");
        }

        using (var pdf = await GetAsync(jar, $"/api/v1/reports/merit-list/export?{query}&armId={seeded.ArmId}&format=pdf"))
        {
            pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
            pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        }

        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exports = await context.AuditEvents.AsNoTracking()
            .Where(audit => audit.Action == "report.export" && audit.ActorAdminId == adminId)
            .Select(audit => audit.EntityId)
            .ToListAsync(TestContext.Current.CancellationToken);
        exports.ShouldBe(["broadsheet", "merit-list"], ignoreOrder: true);
    }

    [Fact]
    public async Task DistributionPerformanceDevelopmentAndFees_ReadTheSameArm()
    {
        RequireDatabase();
        var (jar, _) = await SignInAdminAsync();
        var seeded = await SeedAsync();
        var query = $"termId={seeded.TermId}";

        var distribution = await GetReportAsync(jar, $"/api/v1/reports/grade-distribution?{query}&armId={seeded.ArmId}");
        distribution.Rows.ShouldContain(row => row.Kind == ReportRowKind.Heading && row.Cells[0] == seeded.SubjectName);
        distribution.Rows.ShouldContain(row => row.Kind == ReportRowKind.Data && row.Cells[0] == "B" && row.Cells[2] == "2" && row.Cells[3] == "100%");

        var performance = await GetReportAsync(jar, $"/api/v1/reports/subject-performance?{query}&levelId={seeded.LevelId}");
        var armRow = performance.Rows.Single(row => row.Cells[0] == seeded.ArmName);
        armRow.Cells[1].ShouldBe("72.50"); // (61 + 84) / 2
        armRow.Cells[2].ShouldBe("84");
        armRow.Cells[3].ShouldBe("61");
        armRow.Cells[7].ShouldBe("100%");

        var development = await GetReportAsync(jar, $"/api/v1/reports/development-summary?{query}&armId={seeded.ArmId}");
        development.Rows.ShouldBeEmpty();
        development.Notes.ShouldContain(note => note.Contains("nursery", StringComparison.Ordinal));

        var fees = await GetReportAsync(jar, $"/api/v1/reports/fee-notice-audit?{query}&levelId={seeded.LevelId}");
        fees.Rows.ShouldContain(row => row.Cells[0] == "Pupils with an outstanding figure" && row.Cells[1] == "0");
    }

    [Fact]
    public async Task FeeAudit_ShowsTheTermsLinesAndCountsATypedZero_AndDistributionRefusesArmAndLevelTogether()
    {
        RequireDatabase();
        var (jar, _) = await SignInAdminAsync();
        var seeded = await SeedAsync();
        var label = $"Levy {Guid.NewGuid():N}"[..16];
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fee = SchoolManagement.Domain.Fees.FeeLabel.Create(Guid.CreateVersion7(), SeededClassLevels.PrimarySectionId, label, Interlocked.Increment(ref _nextFeeOrder), SchoolManagement.Domain.Fees.FeeLabelKind.Amount, false).Value;
            context.Add(fee);
            context.Add(SchoolManagement.Domain.Fees.FeeAmount.Create(Guid.CreateVersion7(), fee.Id, seeded.TermId, seeded.LevelId, 12500).Value);
            context.Add(SchoolManagement.Domain.Fees.OutstandingFee.Create(Guid.CreateVersion7(), seeded.ResultSetId, seeded.PupilIds[0], 0).Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var fees = await GetReportAsync(jar, $"/api/v1/reports/fee-notice-audit?termId={seeded.TermId}&levelId={seeded.LevelId}");
        fees.Rows.ShouldContain(row => row.Cells[0] == label && row.Cells[1] == "12,500");
        fees.Rows.ShouldContain(row => row.Cells[0] == "Total fees" && row.Cells[1] == "12,500");
        fees.Rows.ShouldContain(row => row.Cells[0] == "Pupils with an outstanding figure" && row.Cells[1] == "1");

        using var both = await GetAsync(jar, $"/api/v1/reports/grade-distribution?termId={seeded.TermId}&armId={seeded.ArmId}&levelId={seeded.LevelId}");
        both.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DevelopmentSummary_CountsRatingsPerPoint_AndTheUnratedOfTheWholeClass()
    {
        RequireDatabase();
        var (jar, _) = await SignInAdminAsync();
        Guid termId;
        Guid armId;
        string indicatorName;
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var level = await context.ClassLevels.AsNoTracking()
                .FirstAsync(candidate => candidate.SectionId == SeededClassLevels.NurserySectionId, TestContext.Current.CancellationToken);
            var indicator = await (
                    from item in context.DevelopmentIndicators.AsNoTracking()
                    join domain in context.DevelopmentDomains.AsNoTracking() on item.DomainId equals domain.Id
                    where domain.SectionId == SeededClassLevels.NurserySectionId
                        && domain.Status == SchoolManagement.Domain.Settings.DevelopmentDomainStatus.Active
                        && item.Status == SchoolManagement.Domain.Settings.DevelopmentIndicatorStatus.Active
                    orderby domain.DisplayOrder, item.DisplayOrder
                    select new { item.Id, item.Name, domain.RatingScaleId })
                .FirstAsync(TestContext.Current.CancellationToken);
            var point = await context.RatingScalePoints.AsNoTracking()
                .Where(candidate => candidate.RatingScaleId == indicator.RatingScaleId)
                .OrderBy(candidate => candidate.PointOrder)
                .FirstAsync(TestContext.Current.CancellationToken);
            indicatorName = indicator.Name;

            var year = Interlocked.Increment(ref _nextYear);
            var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
            context.Add(session);
            var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", new DateOnly(year, 9, 1), new DateOnly(year, 12, 15)).Value;
            context.Add(term);
            var arm = Arm.Create(Guid.CreateVersion7(), level.Id, session.Id, "A", null, null).Value;
            context.Add(arm);
            var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
            context.Add(resultSet);
            var pupils = new[] { "Obi", "Nnaji" }.Select(surname => Pupil.Create(
                Guid.CreateVersion7(), surname, "Ada", middleName: null, PupilSex.Female, new DateOnly(2022, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value).ToList();
            foreach (var pupil in pupils)
            {
                context.Add(pupil);
                context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 1)).Value);
            }

            context.Add(DevelopmentRating.Create(Guid.CreateVersion7(), resultSet.Id, pupils[0].Id, indicator.Id, point.Id, null).Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id IN (SELECT pupil_id FROM enrolments WHERE arm_id = {arm.Id})",
                TestContext.Current.CancellationToken);
            termId = term.Id;
            armId = arm.Id;
        }

        var report = await GetReportAsync(jar, $"/api/v1/reports/development-summary?termId={termId}&armId={armId}");

        var row = report.Rows.First(candidate => candidate.Kind == ReportRowKind.Data && candidate.Cells[0] == indicatorName);
        row.Cells[1].ShouldBe("1");
        row.Cells[^1].ShouldBe("1"); // two in the class, one rated
        report.Notes.ShouldContain("Out of the 2 pupils enrolled in the class during the term.");
    }

    [Fact]
    public async Task AnnualPromotionAndPupilRecord_ReadTheAnnualRows()
    {
        RequireDatabase();
        var (jar, _) = await SignInAdminAsync();
        var seeded = await SeedAsync();
        Guid sessionId;
        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            sessionId = await context.Terms.Where(term => term.Id == seeded.TermId).Select(term => term.SessionId).SingleAsync(TestContext.Current.CancellationToken);
            foreach (var (pupilId, average, position) in new[] { (seeded.PupilIds[0], 61m, 2), (seeded.PupilIds[1], 84m, 1) })
            {
                context.Add(AnnualResult.Create(
                    sessionId, seeded.ArmId, pupilId, 1, [average, null, null], [(int)average, null, null], (int)average, average, "B", "Good",
                    position, false, 2, "[]", position == 1 ? PromotionOutcome.Promoted : PromotionOutcome.Repeat, DateTimeOffset.UtcNow));
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var annual = await GetReportAsync(jar, $"/api/v1/reports/annual-cumulative?sessionId={sessionId}&armId={seeded.ArmId}");
        annual.Rows.Select(row => row.Cells[1]).ShouldBe(["EZE Chidera", "OKAFOR Chidera"]);
        annual.Rows[0].Cells[^1].ShouldBe("Promoted (proposed)");

        var promotion = await GetReportAsync(jar, $"/api/v1/reports/promotion-list?sessionId={sessionId}&outcome=Repeat");
        promotion.Rows.Select(row => row.Cells[0]).ShouldBe(["OKAFOR Chidera"]);

        var record = await GetReportAsync(jar, $"/api/v1/reports/pupil-record?pupilId={seeded.PupilIds[1]}");
        record.Rows.ShouldContain(row => row.Kind == ReportRowKind.Data && row.Cells[0] == "First Term" && row.Cells[2] == "84.00");
        record.Rows.ShouldContain(row => row.Kind == ReportRowKind.Subtotal && row.Cells[0] == "Annual" && row.Cells[6] == "Promoted (proposed)");

        using var badOutcome = await GetAsync(jar, $"/api/v1/reports/promotion-list?sessionId={sessionId}&outcome=Expelled");
        badOutcome.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Broadsheet_WithoutAnArm_IsAValidationError()
    {
        RequireDatabase();
        var (jar, _) = await SignInAdminAsync();

        using var response = await GetAsync(jar, $"/api/v1/reports/broadsheet?termId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    private sealed record Seeded(Guid TermId, Guid ArmId, string ArmName, Guid LevelId, string SubjectName, Guid ResultSetId, IReadOnlyList<Guid> PupilIds);

    /// <summary>An Approved, computed arm: Eze first, Okafor second, one subject.</summary>
    private async Task<Seeded> SeedAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var level = await context.ClassLevels.AsNoTracking()
            .FirstAsync(candidate => candidate.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken);

        var year = Interlocked.Increment(ref _nextYear);
        var session = AcademicSession.Create(Guid.CreateVersion7(), $"{year}/{year + 1}", new DateOnly(year, 9, 1), new DateOnly(year + 1, 7, 31)).Value;
        context.Add(session);
        var term = Term.Create(Guid.CreateVersion7(), session.Id, 1, "First Term", new DateOnly(year, 9, 1), new DateOnly(year, 12, 15)).Value;
        context.Add(term);
        var arm = Arm.Create(Guid.CreateVersion7(), level.Id, session.Id, "A", null, null).Value;
        context.Add(arm);
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), arm.Id, term.Id).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        resultSet.MarkComputed(null, DateTimeOffset.UtcNow, 2);
        context.Add(resultSet);

        var subjectName = $"Maths {Guid.NewGuid():N}"[..20];
        var subject = SchoolManagement.Domain.Subjects.Subject.Create(Guid.CreateVersion7(), subjectName, null, null).Value;
        context.Add(subject);
        context.Add(SchoolManagement.Domain.Subjects.SubjectMapping.Create(Guid.CreateVersion7(), subject.Id, level.Id, session.Id, term.Id, 1).Value);

        var pupilIds = new List<Guid>();
        foreach (var (surname, position, total) in new[] { ("Okafor", 2, 61), ("Eze", 1, 84) })
        {
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 1, 1), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
            context.Add(pupil);
            context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(year, 9, 1)).Value);
            pupilIds.Add(pupil.Id);
            context.Add(SubjectResultLine.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, subject.Id, total / 2, total - (total / 2), total, "B", "Good", position, false, true));
            context.Add(PupilTermResult.Create(Guid.CreateVersion7(), resultSet.Id, pupil.Id, 1, 100, total, total, "B", position, false, 2, position, false, 2));
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        // Pending pupils are hidden by the model's query filter; a pupil with results is active.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = {nameof(PupilStatus.Active)} WHERE id IN (SELECT pupil_id FROM enrolments WHERE arm_id = {arm.Id})",
            TestContext.Current.CancellationToken);
        return new Seeded(term.Id, arm.Id, ArmDisplayName.Compose(level.Name, "A"), level.Id, subjectName, resultSet.Id, pupilIds);
    }

    private async Task<ReportDto> GetReportAsync(CookieJar jar, string url)
    {
        using var response = await GetAsync(jar, url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (await response.Content.ReadFromJsonAsync<ReportDto>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    private async Task<(CookieJar Jar, Guid AccountId)> SignInAdminAsync()
    {
        var (accountId, email) = await AdminAccountSeeder.SeedAsync(Fixture, mustChangePassword: false, email: $"admin-{Guid.NewGuid():N}@example.com");
        var jar = new CookieJar();
        using (var csrf = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf"))
        {
            jar.Capture(await Client.SendAsync(csrf, TestContext.Current.CancellationToken));
        }

        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-in")
        {
            Content = JsonContent.Create(new SchoolManagement.Application.Auth.SignIn.SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(signIn);
        var response = await Client.SendAsync(signIn, TestContext.Current.CancellationToken);
        jar.Capture(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (jar, accountId);
    }

    private async Task<HttpResponseMessage> GetAsync(CookieJar jar, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
