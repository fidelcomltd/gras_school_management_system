using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0060, spec 4.2.1 and 4.2.2: a grant authorises only "in the session the target belongs to". Each test signs in
/// two regular accounts holding the same SCHOOL-WIDE role, one assigned in last session and one in the target's own
/// session, and proves only the second gets through (with a 200, so a broken lookup cannot pass as "not refused"). Arm-list grants cannot cross sessions (an assignment's arms must
/// belong to its session), so the school-wide grant is the case that leaked. One target per enforcement path: a
/// declarative arm-scoped route, a handler-guarded pupil read, the register list, and a report.
/// </summary>
public sealed class SessionBoundaryAuthorizationTests(ApiTestFixture fixture) : IntegrationTestBase(fixture)
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    [Fact]
    public async Task ArmScopedRoute_SchoolWideGrantInAnotherSession_Returns403()
    {
        RequireDatabase();
        var seeded = await SeedAsync();

        await AssertOnlyTargetSessionPassesAsync(seeded, Privileges.Results.View, $"/api/v1/arms/{seeded.ArmId}/readiness?termId={seeded.TermId}");
    }

    [Fact]
    public async Task PupilRead_SchoolWideGrantInAnotherSession_Returns403()
    {
        RequireDatabase();
        var seeded = await SeedAsync();

        await AssertOnlyTargetSessionPassesAsync(seeded, Privileges.Pupil.View, $"/api/v1/pupils/{seeded.PupilId}");
    }

    [Fact]
    public async Task PupilList_SchoolWideGrantInAnotherSession_Returns403()
    {
        // The register's pupils belong to the active session (spec 4.2.1: "arm of record for the active term").
        RequireDatabase();
        var seeded = await SeedAsync();

        await AssertOnlyTargetSessionPassesAsync(seeded, Privileges.Pupil.View, "/api/v1/pupils");
    }

    [Fact]
    public async Task Report_SchoolWideGrantInAnotherSession_Returns403()
    {
        RequireDatabase();
        var seeded = await SeedAsync();

        await AssertOnlyTargetSessionPassesAsync(
            seeded, Privileges.Report.View, $"/api/v1/reports/broadsheet?termId={seeded.TermId}&armId={seeded.ArmId}");
    }

    private async Task AssertOnlyTargetSessionPassesAsync(Seeded seeded, string privilege, string url)
    {
        var roleId = await SeedRoleAsync(privilege);

        var lastSession = await SignInWithSchoolWideGrantAsync(roleId, seeded.LastSessionId);
        using (var refused = await GetAsync(url, lastSession))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a grant in last session must not reach a target in this one");
        }

        var thisSession = await SignInWithSchoolWideGrantAsync(roleId, seeded.TargetSessionId);
        using var allowed = await GetAsync(url, thisSession);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK, "a grant in the target's own session must reach it");
    }

    private sealed record Seeded(Guid LastSessionId, Guid TargetSessionId, Guid TermId, Guid ArmId, Guid PupilId);

    private async Task<Seeded> SeedAsync()
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(level => level.SectionId == SeededClassLevels.PrimarySectionId, TestContext.Current.CancellationToken)).Id;

        var last = AcademicSession.Create(Guid.CreateVersion7(), "2025/2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 7, 31)).Value;
        var target = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        target.Activate();
        context.AddRange(last, target);

        var term = Term.Create(Guid.CreateVersion7(), target.Id, 1, "First Term", new DateOnly(2026, 9, 14), new DateOnly(2026, 12, 11)).Value;
        var arm = Arm.Create(Guid.CreateVersion7(), levelId, target.Id, "A", null, null).Value;
        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "O'Brien", "Chidera", middleName: null, PupilSex.Female, new DateOnly(2018, 3, 4), asOfDate: new DateOnly(2026, 9, 9),
            nationality: null, "Lagos", "Ikeja", "3 Allen Avenue, Ikeja", previousSchool: null, previousClass: null, otherInformation: null).Value;
        context.AddRange(term, arm, pupil);
        context.Add(Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, new DateOnly(2026, 9, 14)).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET registration_number = {"GRAS/2026/0060"}, status = {PupilStatus.Active.ToString()} WHERE id = {pupil.Id}",
            TestContext.Current.CancellationToken);

        return new Seeded(last.Id, target.Id, term.Id, arm.Id, pupil.Id);
    }

    private async Task<Guid> SeedRoleAsync(string privilege)
    {
        await using var scope = Fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var role = Role.Create(Guid.CreateVersion7(), $"Role-{Guid.NewGuid():N}", null, [privilege]).Value;
        context.Add(role);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return role.Id;
    }

    private async Task<CookieJar> SignInWithSchoolWideGrantAsync(Guid roleId, Guid sessionId)
    {
        var (accountId, email, _) = await AdminAccountSeeder.SeedRegularAsync(Fixture, mustChangePassword: false);

        await using (var scope = Fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Add(RoleAssignment.Create(Guid.CreateVersion7(), accountId, roleId, sessionId, ScopeType.SchoolWide, [], accountId).Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var jar = new CookieJar();
        using (var csrf = await GetAsync(CsrfUrl, jar))
        {
            csrf.EnsureSuccessStatusCode();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = System.Net.Http.Json.JsonContent.Create(new SignInCommand(email, AdminAccountSeeder.Password)),
        };
        jar.ApplyWithCsrf(request);
        using var signIn = await Client.SendAsync(request, TestContext.Current.CancellationToken);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        jar.Capture(signIn);
        return jar;
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
