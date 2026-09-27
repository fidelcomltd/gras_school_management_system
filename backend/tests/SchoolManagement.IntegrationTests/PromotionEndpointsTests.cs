using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Auth.SignIn;
using SchoolManagement.Application.Promotion;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Spec 6.3.7 end to end: the preview's proposals, default arms and blockers; the one-transaction commit with its
/// <c>promotion.decide</c> and on-trial rules; and reversal, both allowed and refused once a mark exists in the new session.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class PromotionEndpointsTests : IAsyncLifetime
{
    private const string CsrfUrl = "/api/v1/auth/csrf";
    private const string SignInUrl = "/api/v1/auth/sign-in";

    private static readonly Guid Primary2Id = new("00000000-0000-0000-0000-000000000315");
    private static readonly Guid Primary3Id = new("00000000-0000-0000-0000-000000000316");
    private static readonly Guid Primary6Id = new("00000000-0000-0000-0000-000000000319");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiTestFixture _fixture;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private FakeEffectivePrivilegeProvider _grants = null!;

    public PromotionEndpointsTests(ApiTestFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            return;
        }

        await _fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);
        _grants = new FakeEffectivePrivilegeProvider();
        _factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEffectivePrivilegeProvider>();
            services.AddSingleton<IEffectivePrivilegeProvider>(_grants);
        }));
        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return _factory?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Preview_Commit_Reverse_EndToEnd()
    {
        RequireDatabase();
        var seeded = await SeedAsync(thirdTermClosed: true, coreSubjectsChosen: true, primary3Arms: 2);
        var decider = await SignInWithGrantsAsync(superAdmin: true, Privileges.Promotion.Run, Privileges.Promotion.Decide, Privileges.Promotion.Reverse);

        var preview = await ReadAsync<PromotionPreviewDto>(await GetAsync($"/api/v1/sessions/{seeded.SourceId}/promotion/preview", decider));
        preview.Blockers.ShouldBeEmpty();
        preview.TargetSession!.Id.ShouldBe(seeded.TargetId);
        preview.CanDecide.ShouldBeTrue();
        preview.Rows.Count.ShouldBe(4);
        var strong = preview.Rows.Single(row => row.PupilId == seeded.Strong);
        strong.ProposedOutcome.ShouldBe(PromotionDecisionOutcome.Promoted);
        strong.AnnualAverage.ShouldBe(75m);
        strong.CoreResults.Single().Passed.ShouldBe(true);
        strong.ProposedTargetArmId.ShouldBe(seeded.Primary3A);
        preview.Rows.Single(row => row.PupilId == seeded.Weak).ProposedOutcome.ShouldBe(PromotionDecisionOutcome.Repeat);
        preview.Rows.Single(row => row.PupilId == seeded.Weak).ProposedTargetArmId.ShouldBe(seeded.Primary2A);
        preview.Rows.Single(row => row.PupilId == seeded.NoResult).ProposedOutcome.ShouldBeNull();
        preview.Rows.Single(row => row.PupilId == seeded.Leaver).ProposedOutcome.ShouldBe(PromotionDecisionOutcome.Graduated);
        preview.Excluded.Single().PupilId.ShouldBe(seeded.Withdrawn);
        preview.TargetArms.Count.ShouldBe(3);

        // Changing a proposal needs promotion.decide.
        var runner = await SignInWithGrantsAsync(superAdmin: false, Privileges.Promotion.Run);
        var overridden = Decisions(seeded, weak: new(seeded.Weak, PromotionDecisionOutcome.Promoted, seeded.Primary3B, null));
        (await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", runner, overridden, Guid.NewGuid().ToString())).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        // On trial needs a reason.
        var noReason = Decisions(seeded, weak: new(seeded.Weak, PromotionDecisionOutcome.PromotedOnTrial, seeded.Primary3B, null));
        (await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", decider, noReason, Guid.NewGuid().ToString())).StatusCode
            .ShouldBe(HttpStatusCode.UnprocessableEntity);

        // No decisions at all is a validation failure, never a 500.
        (await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", decider, new { targetSessionId = seeded.TargetId }, Guid.NewGuid().ToString())).StatusCode
            .ShouldBe(HttpStatusCode.UnprocessableEntity);

        // A pupil left out is refused whole: nothing applied.
        var partial = Decisions(seeded) with { Decisions = Decisions(seeded).Decisions.Skip(1).ToList() };
        (await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", decider, partial, Guid.NewGuid().ToString())).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);

        var command = Decisions(seeded, weak: new(seeded.Weak, PromotionDecisionOutcome.PromotedOnTrial, seeded.Primary3B, "Strong Third Term after illness."));
        var committed = await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", decider, command, Guid.NewGuid().ToString());
        committed.StatusCode.ShouldBe(HttpStatusCode.OK, await committed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var batch = await ReadAsync<PromotionBatchDto>(committed);
        (batch.Promoted, batch.PromotedOnTrial, batch.Repeated, batch.Graduated).ShouldBe((1, 1, 1, 1));

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var strongEnrolments = await context.Set<Enrolment>().AsNoTracking().Where(row => row.PupilId == seeded.Strong).ToListAsync(TestContext.Current.CancellationToken);
            strongEnrolments.Single(row => row.ArmId == seeded.Primary2Source).EffectiveTo.ShouldBe(seeded.SourceEnd);
            strongEnrolments.Single(row => row.EffectiveTo == null).ArmId.ShouldBe(seeded.Primary3A);
            strongEnrolments.Single(row => row.EffectiveTo == null).EffectiveFrom.ShouldBe(seeded.TargetStart);
            (await context.Set<Pupil>().AsNoTracking().SingleAsync(pupil => pupil.Id == seeded.Leaver, TestContext.Current.CancellationToken)).Status
                .ShouldBe(PupilStatus.Graduated);
            (await context.Set<Enrolment>().AnyAsync(row => row.PupilId == seeded.Leaver && row.EffectiveTo == null, TestContext.Current.CancellationToken)).ShouldBeFalse();
            var graduation = await context.Set<PupilStatusChange>().AsNoTracking().SingleAsync(row => row.PupilId == seeded.Leaver, TestContext.Current.CancellationToken);
            (graduation.FromStatus, graduation.ToStatus).ShouldBe((PupilStatus.Active, PupilStatus.Graduated));
            (await context.Set<PromotionDecision>().AsNoTracking().SingleAsync(row => row.PupilId == seeded.Weak, TestContext.Current.CancellationToken)).Reason
                .ShouldBe("Strong Third Term after illness.");
            (await context.AuditEvents.AnyAsync(audit => audit.Action == Privileges.Promotion.Run, TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        // Run twice: the preview shows the batch and a second commit is refused.
        var after = await ReadAsync<PromotionPreviewDto>(await GetAsync($"/api/v1/sessions/{seeded.SourceId}/promotion/preview", decider));
        after.CommittedBatch!.Id.ShouldBe(batch.Id);
        after.Blockers.Single().Code.ShouldBe("promotion.already_run");
        (await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", decider, command, Guid.NewGuid().ToString())).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);

        // promotion.reverse alone is not enough: spec 6.3.7 reserves reversal to a Super Admin.
        var notSuper = await SignInWithGrantsAsync(superAdmin: false, Privileges.Promotion.Run, Privileges.Promotion.Reverse);
        (await PostAsync($"/api/v1/promotion-batches/{batch.Id}/reverse", notSuper, new { reason = "Committed into the wrong session." })).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        var reversed = await PostAsync($"/api/v1/promotion-batches/{batch.Id}/reverse", decider, new { reason = "Committed into the wrong session." });
        reversed.StatusCode.ShouldBe(HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        (await ReadAsync<PromotionBatchDto>(reversed)).State.ShouldBe(PromotionBatchState.Reversed);

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var strongEnrolments = await context.Set<Enrolment>().AsNoTracking().Where(row => row.PupilId == seeded.Strong).ToListAsync(TestContext.Current.CancellationToken);
            strongEnrolments.Single().ArmId.ShouldBe(seeded.Primary2Source);
            strongEnrolments.Single().EffectiveTo.ShouldBeNull();
            (await context.Set<Pupil>().AsNoTracking().SingleAsync(pupil => pupil.Id == seeded.Leaver, TestContext.Current.CancellationToken)).Status
                .ShouldBe(PupilStatus.Active);
            (await context.PromotionBatches.AsNoTracking().SingleAsync(row => row.Id == batch.Id, TestContext.Current.CancellationToken)).State
                .ShouldBe(PromotionBatchState.Reversed);
        }

        // After reversal the session can be promoted again.
        (await ReadAsync<PromotionPreviewDto>(await GetAsync($"/api/v1/sessions/{seeded.SourceId}/promotion/preview", decider))).Blockers.ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_ListsEveryUnmetPrecondition_AndCommitIsRefused()
    {
        RequireDatabase();
        var seeded = await SeedAsync(thirdTermClosed: false, coreSubjectsChosen: false, primary3Arms: 0);
        var jar = await SignInWithGrantsAsync(superAdmin: false, Privileges.Promotion.Run);

        var preview = await ReadAsync<PromotionPreviewDto>(await GetAsync($"/api/v1/sessions/{seeded.SourceId}/promotion/preview", jar));

        preview.Blockers.Select(blocker => blocker.Code).ShouldBe(
            ["promotion.third_term_not_closed", "promotion.result_rules_incomplete", "promotion.receiving_level_without_arm"], ignoreOrder: true);
        preview.Blockers.Single(blocker => blocker.Code == "promotion.receiving_level_without_arm").Message
            .ShouldBe($"Primary 3 has no arm in {seeded.TargetName}. Create at least one arm before running promotion.");
        (await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", jar, Decisions(seeded), Guid.NewGuid().ToString())).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reverse_OnceAMarkIsEnteredInTheNewSession_IsRefusedWithTheSpecMessage()
    {
        RequireDatabase();
        var seeded = await SeedAsync(thirdTermClosed: true, coreSubjectsChosen: true, primary3Arms: 1);
        var jar = await SignInWithGrantsAsync(superAdmin: true, Privileges.Promotion.Run, Privileges.Promotion.Decide, Privileges.Promotion.Reverse);
        var committed = await PostAsync($"/api/v1/sessions/{seeded.SourceId}/promotion", jar, Decisions(seeded), Guid.NewGuid().ToString());
        committed.StatusCode.ShouldBe(HttpStatusCode.OK, await committed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var batch = await ReadAsync<PromotionBatchDto>(committed);

        await using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var term = Term.Create(Guid.CreateVersion7(), seeded.TargetId, 1, "First Term", seeded.TargetStart, seeded.TargetStart.AddMonths(3)).Value;
            context.Add(term);
            var resultSet = ResultSet.Create(Guid.CreateVersion7(), seeded.Primary3A, term.Id).Value;
            context.Add(resultSet);
            context.Add(SubjectScore.Create(Guid.CreateVersion7(), resultSet.Id, seeded.Strong, seeded.SubjectId, term.Id, "{}", 50, false).Value);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var refused = await PostAsync($"/api/v1/promotion-batches/{batch.Id}/reverse", jar, new { reason = "Committed into the wrong session." });

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(
            $"Marks have already been entered in {seeded.TargetName}. This promotion cannot be reversed. Move individual pupils between arms instead.");
    }

    private void RequireDatabase()
    {
        if (!_fixture.IsDatabaseAvailable)
        {
            Assert.Skip(_fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    private static CommitPromotionCommand Decisions(Seeded seeded, PromotionDecisionInput? weak = null) =>
        new(seeded.SourceId, seeded.TargetId,
        [
            new(seeded.Strong, PromotionDecisionOutcome.Promoted, seeded.Primary3A, null),
            weak ?? new(seeded.Weak, PromotionDecisionOutcome.Repeat, seeded.Primary2A, null),
            new(seeded.NoResult, PromotionDecisionOutcome.Repeat, seeded.Primary2A, null),
            new(seeded.Leaver, PromotionDecisionOutcome.Graduated, null, null),
        ]);

    private sealed record Seeded(
        Guid SourceId, Guid TargetId, string TargetName, DateOnly SourceEnd, DateOnly TargetStart, Guid Primary2Source, Guid Primary2A,
        Guid Primary3A, Guid Primary3B, Guid SubjectId, Guid Strong, Guid Weak, Guid NoResult, Guid Leaver, Guid Withdrawn);

    private async Task<Seeded> SeedAsync(bool thirdTermClosed, bool coreSubjectsChosen, int primary3Arms)
    {
        await using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 23)).Value;
        source.Activate();
        var target = AcademicSession.Create(Guid.CreateVersion7(), "2027/2028", new DateOnly(2027, 9, 13), new DateOnly(2028, 7, 21)).Value;
        context.AddRange(source, target);

        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            var term = Term.Create(
                Guid.CreateVersion7(), source.Id, ordinal, ordinal switch { 1 => "First Term", 2 => "Second Term", _ => "Third Term" },
                source.StartDate.AddMonths(3 * (ordinal - 1)), source.StartDate.AddMonths(3 * (ordinal - 1) + 2)).Value;
            if (ordinal < 3 || thirdTermClosed)
            {
                typeof(Term).GetProperty(nameof(Term.State))!.SetValue(term, TermState.Closed);
            }

            context.Add(term);
        }

        var p2Source = Arm.Create(Guid.CreateVersion7(), Primary2Id, source.Id, "A", null, null).Value;
        var p6Source = Arm.Create(Guid.CreateVersion7(), Primary6Id, source.Id, "A", null, null).Value;
        var p2Target = Arm.Create(Guid.CreateVersion7(), Primary2Id, target.Id, "A", null, null).Value;
        var p3a = Arm.Create(Guid.CreateVersion7(), Primary3Id, target.Id, "A", null, null).Value;
        var p3b = Arm.Create(Guid.CreateVersion7(), Primary3Id, target.Id, "B", null, null).Value;
        context.AddRange(p2Source, p6Source, p2Target);
        if (primary3Arms >= 1)
        {
            context.Add(p3a);
        }

        if (primary3Arms >= 2)
        {
            context.Add(p3b);
        }

        var subject = SchoolManagement.Domain.Subjects.Subject.Create(Guid.CreateVersion7(), "Promotion Maths", null, null).Value;
        context.Add(subject);

        Pupil AddPupil(string surname, Arm arm, PupilStatus status = PupilStatus.Active)
        {
            var pupil = Pupil.Create(
                Guid.CreateVersion7(), surname, "Ada", middleName: null, PupilSex.Female, new DateOnly(2018, 3, 4), asOfDate: new DateOnly(2026, 9, 9),
                nationality: null, "Anambra", "Awka South", "14 Zik Avenue, Awka", previousSchool: null, previousClass: null, otherInformation: null).Value;
            typeof(Pupil).GetProperty(nameof(Pupil.RegistrationNumber))!.SetValue(pupil, $"GRA/2026/{surname[..4].ToUpperInvariant()}");
            typeof(Pupil).GetProperty(nameof(Pupil.Status))!.SetValue(pupil, status);
            context.Add(pupil);
            var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, source.StartDate).Value;
            if (status != PupilStatus.Active)
            {
                enrolment.Close(source.StartDate.AddMonths(2));
            }

            context.Add(enrolment);
            return pupil;
        }

        void AddAnnual(Pupil pupil, Arm arm, decimal average, PromotionOutcome proposed) =>
            context.Add(AnnualResult.Create(
                source.Id, arm.Id, pupil.Id, 3, [average, average, average], [(int)average, (int)average, (int)average], (int)average * 3, average,
                "C", "Credit", 1, false, 1, JsonSerializer.Serialize(new[] { new AnnualSubjectResult(subject.Id, [(int)average], average, "C", 3) }, Json),
                proposed, DateTimeOffset.UtcNow));

        var strong = AddPupil("Okafor", p2Source);
        var weak = AddPupil("Bello", p2Source);
        var noResult = AddPupil("Chukwu", p2Source);
        var leaver = AddPupil("Danjuma", p6Source);
        var withdrawn = AddPupil("Ezeani", p2Source, PupilStatus.Withdrawn);
        AddAnnual(strong, p2Source, 75m, PromotionOutcome.Promoted);
        AddAnnual(weak, p2Source, 30m, PromotionOutcome.Repeat);
        AddAnnual(leaver, p6Source, 35m, PromotionOutcome.Repeat);

        if (coreSubjectsChosen)
        {
            var rules = await context.Set<ResultRules>().SingleAsync(TestContext.Current.CancellationToken);
            rules.Update(
                rules.AnnualMethod, rules.WeightFirst, rules.WeightSecond, rules.WeightThird, rules.PrimaryPositionScope, rules.ShowLevelPosition,
                rules.TieBreakRule, rules.PassMark, rules.PromotionThreshold, requireCorePass: true, [subject.Id], rules.MinSubjectsForPosition);
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seeded(
            source.Id, target.Id, target.Name, source.EndDate, target.StartDate, p2Source.Id, p2Target.Id, p3a.Id, p3b.Id, subject.Id,
            strong.Id, weak.Id, noResult.Id, leaver.Id, withdrawn.Id);
    }

    private async Task<CookieJar> SignInWithGrantsAsync(bool superAdmin, params string[] privileges)
    {
        var email = $"promo-{Guid.NewGuid():N}@example.com";
        var accountId = superAdmin
            ? (await AdminAccountSeeder.SeedAsync(_fixture, email: email)).AccountId
            : (await AdminAccountSeeder.SeedRegularAsync(_fixture, email: email)).AccountId;
        _grants.SetGrants(
            accountId.ToString("D", CultureInfo.InvariantCulture),
            privileges.Select(privilege => new PrivilegeGrant(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)).ToArray());
        var jar = new CookieJar();
        await GetAsync(CsrfUrl, jar);
        (await PostAsync(SignInUrl, jar, new SignInCommand(email, AdminAccountSeeder.Password))).StatusCode.ShouldBe(HttpStatusCode.OK);
        return jar;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        where T : class
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue(body);
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CookieJar jar)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        jar.Apply(request);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string url, CookieJar jar, T payload, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload, options: Json) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        jar.ApplyWithCsrf(request);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        jar.Capture(response);
        return response;
    }
}
