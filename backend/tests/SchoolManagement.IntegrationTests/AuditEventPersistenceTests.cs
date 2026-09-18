using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0048's own acceptance criteria, proven directly against the persistence mechanism rather
/// than through any one feature's endpoint: the success path joins the ambient transaction (commits
/// and rolls back WITH the change it records), <c>actor_label</c> is captured at write time, and a
/// privilege-middleware rejection — which runs entirely outside any MediatR pipeline, so it has no
/// ambient transaction to ride at all — is durable the same way a command-level rejection is.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class AuditEventPersistenceTests(ApiTestFixture fixture)
{
    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    /// <summary>
    /// A successful command's audit row and its entity change live or die together — proven by
    /// driving <see cref="IUnitOfWork"/> directly rather than reconstructing a full command, since
    /// that IS the mechanism this criterion is about.
    /// </summary>
    [Fact]
    public async Task SuccessPath_AuditRowCommitsInTheSameTransactionAsTheChange()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sections = scope.ServiceProvider.GetRequiredService<ISectionRepository>();
        var auditEvents = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var sectionId = Guid.CreateVersion7();
        var section = Section.Create(sectionId, $"Success {sectionId:N}"[..30]).Value;
        var entityId = sectionId.ToString("D");

        var result = await unitOfWork.ExecuteAtomicallyAsync(
            async ct =>
            {
                await sections.AddAsync(section, ct);
                await auditEvents.AddAsync(
                    AuditEvent.Create(
                        timeProvider.GetUtcNow(), null, "System", "section.create", "section",
                        entityId, AuditOutcome.Success, null, null, null, null, null),
                    ct);
                return Result.Success();
            },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        await using var verifyScope = fixture.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await context.Sections.AsNoTracking()
                .AnyAsync(s => s.Id == sectionId, TestContext.Current.CancellationToken))
            .ShouldBeTrue();

        var persistedEvent = await context.AuditEvents.AsNoTracking()
            .SingleOrDefaultAsync(e => e.EntityId == entityId, TestContext.Current.CancellationToken);

        persistedEvent.ShouldNotBeNull();
        persistedEvent.Outcome.ShouldBe(AuditOutcome.Success);
    }

    /// <summary>
    /// The other half of the same criterion: a handler that throws mid-way — rather than returning a
    /// failure <see cref="Result"/> — leaves NEITHER the entity NOR the audit row behind. Spec
    /// 6.1.12: "If the transaction rolls back the event disappears with it, so a gap in the log is a
    /// failure, not a silent omission" only means anything if this holds.
    /// </summary>
    [Fact]
    public async Task ThrowingMidHandler_RollsBackBothTheChangeAndTheAuditRow()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sections = scope.ServiceProvider.GetRequiredService<ISectionRepository>();
        var auditEvents = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var sectionId = Guid.CreateVersion7();
        var section = Section.Create(sectionId, $"Throws {sectionId:N}"[..30]).Value;
        var entityId = sectionId.ToString("D");

        await Should.ThrowAsync<InvalidOperationException>(() => unitOfWork.ExecuteAtomicallyAsync<Result>(
            async ct =>
            {
                await sections.AddAsync(section, ct);
                await auditEvents.AddAsync(
                    AuditEvent.Create(
                        timeProvider.GetUtcNow(), null, "System", "section.create", "section",
                        entityId, AuditOutcome.Success, null, null, null, null, null),
                    ct);

                throw new InvalidOperationException("Simulated mid-handler failure (TASK-0048 test).");
            },
            TestContext.Current.CancellationToken));

        await using var verifyScope = fixture.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await context.Sections.AsNoTracking()
                .AnyAsync(s => s.Id == sectionId, TestContext.Current.CancellationToken))
            .ShouldBeFalse();

        (await context.AuditEvents.AsNoTracking()
                .AnyAsync(e => e.EntityId == entityId, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }

    /// <summary>
    /// Spec 6.1.12: "Staff name and email captured at the time, so the entry stays readable after the
    /// account is renamed." Audits a change, renames the account, re-reads the row: the label must
    /// still show the ORIGINAL name and email, never joined against the account's current state.
    /// </summary>
    [Fact]
    public async Task ActorLabel_IsCapturedAtWriteTime_NotJoinedOnRead()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        const string originalName = "Original Name Before Rename";
        var (accountId, originalEmail, _) = await AdminAccountSeeder.SeedRegularAsync(
            fixture, staffName: originalName);

        const string action = "test.actor_label_probe";

        await using (var writeScope = fixture.CreateScope())
        {
            var auditSink = writeScope.ServiceProvider.GetRequiredService<ISystemAuditSink>();
            var context = writeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await auditSink.RecordAsync(
                action,
                "admin_account",
                accountId.ToString("D"),
                metadata: null,
                actorAdminId: accountId.ToString("D"),
                TestContext.Current.CancellationToken);

            // RecordAsync joins the change tracker only — this test is not inside
            // UnitOfWorkBehavior, so it commits explicitly, standing in for "the ambient transaction
            // committed" that a real successful command would have done.
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var renameScope = fixture.CreateScope())
        {
            var context = renameScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await context.AdminAccounts
                .SingleAsync(a => a.Id == accountId, TestContext.Current.CancellationToken);

            account.UpdateDetails("Renamed Person", "renamed-" + originalEmail, account.Phone!)
                .IsSuccess.ShouldBeTrue();

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyScope = fixture.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persisted = await verifyContext.AuditEvents.AsNoTracking()
            .SingleAsync(e => e.Action == action, TestContext.Current.CancellationToken);

        persisted.ActorLabel.ShouldContain(originalName);
        persisted.ActorLabel.ShouldContain(originalEmail);
        persisted.ActorLabel.ShouldNotContain("Renamed Person");
    }

    /// <summary>
    /// The card's fourth named criterion: a rejection from <c>PrivilegeAuthorizationHandler</c> —
    /// authorization middleware, entirely outside any MediatR pipeline, with no ambient transaction
    /// to roll back OR to ride — is durable too, through the REAL, DI-registered
    /// <c>IAuthorizationAuditSink</c> (never swapped for <c>FakeAuthorizationAuditSink</c> here,
    /// unlike every test in <c>PrivilegeAuthorizationTests</c>).
    /// </summary>
    [Fact]
    public async Task PrivilegeMiddlewareRejection_PersistsADurableRejectedAuditEventRow()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var grants = new FakeEffectivePrivilegeProvider();
        const string userId = "durable-rejection-probe";
        grants.SetGrants(
            userId,
            new PrivilegeGrant(Privileges.Pupil.View, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null));

        await using var factory = fixture.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    configureOptions: null);

            services.RemoveAll<IEffectivePrivilegeProvider>();
            services.AddSingleton<IEffectivePrivilegeProvider>(grants);

            // IAuthorizationAuditSink is deliberately LEFT AS THE REAL, DI-registered
            // AuthorizationAuditSink — proving that one persists is this test's entire point.
        }));

        using var client = factory.CreateClient();
        var armId = Guid.CreateVersion7();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/v1/reference/arms/{armId}/secure", UriKind.Relative));
        request.Headers.Add("X-Test-User-Id", userId);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var persisted = await context.AuditEvents.AsNoTracking()
            .Where(e => e.Action == Privileges.Arm.View && e.Outcome == AuditOutcome.Rejected)
            .ToListAsync(TestContext.Current.CancellationToken);

        persisted.ShouldHaveSingleItem();
    }
}
