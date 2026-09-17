using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0070's two database-enforced guarantees, proven directly against the persistence mechanism
/// (the same technique <c>EnrolmentPersistenceTests</c> uses for the enrolment one-open-row
/// invariant), plus the fresh-database seed proof named as the card's own acceptance criterion.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class SubjectPersistenceTests(ApiTestFixture fixture)
{
    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    // Spec 6.6.3: "Unique on (subject_id, class_level_id, term_id) where status is active" — a
    // partial index. Proven by attempting a SECOND active mapping for the same triple and expecting
    // the database to reject it; a domain-only check could never prove this, because nothing in a
    // single SubjectMapping instance can see a sibling row.
    [Fact]
    public async Task SecondActiveMapping_OnTheSameSubjectLevelTerm_ViolatesThePartialUniqueIndex()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (subjectId, levelId, sessionId, termId) = await SeedSubjectLevelAndTermAsync();

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = SubjectMapping.Create(Guid.CreateVersion7(), subjectId, levelId, sessionId, termId, 1).Value;
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = SubjectMapping.Create(Guid.CreateVersion7(), subjectId, levelId, sessionId, termId, 2).Value;
        secondContext.Add(second);

        await Should.ThrowAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // The positive control for the same index: once the first row is ENDED, a second active mapping
    // for the same triple is not a duplicate any more and must succeed. Without this, a bug that
    // rejected every second row for ANY reason would still make the test above pass.
    [Fact]
    public async Task SecondMapping_OnTheSameSubjectLevelTerm_SucceedsOnceTheFirstIsEnded()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (subjectId, levelId, sessionId, termId) = await SeedSubjectLevelAndTermAsync();

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = SubjectMapping.Create(Guid.CreateVersion7(), subjectId, levelId, sessionId, termId, 1).Value;
            first.End();
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = SubjectMapping.Create(Guid.CreateVersion7(), subjectId, levelId, sessionId, termId, 1).Value;
        secondContext.Add(second);

        await Should.NotThrowAsync(() => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // Spec 6.6.4 / TASK-0070 AC-4: unique on (arm_id, subject_id, term_id) REGARDLESS OF MODE — an
    // include followed by an exclude on the identical triple must collide at the database exactly as
    // two includes would, proving the single ordinary (non-partial) index is what backstops "not
    // order-resolved" at the storage layer, not merely the handler's friendly pre-check.
    [Fact]
    public async Task SecondException_OnTheSameArmSubjectTerm_ViolatesTheUniqueIndex_EvenInTheOppositeMode()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (subjectId, levelId, sessionId, termId) = await SeedSubjectLevelAndTermAsync();
        var armId = await SeedArmAsync(levelId, sessionId);

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var include = SubjectMappingException.Create(
                Guid.CreateVersion7(), armId, subjectId, termId, SubjectExceptionMode.Include, "Seed reason").Value;
            context.Add(include);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exclude = SubjectMappingException.Create(
            Guid.CreateVersion7(), armId, subjectId, termId, SubjectExceptionMode.Exclude, "Second reason").Value;
        secondContext.Add(exclude);

        await Should.ThrowAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // The positive control: once the first row is DELETED (this entity has no status — withdrawing
    // it means removing the row), a new exception on the same triple, in either mode, succeeds.
    [Fact]
    public async Task SecondException_OnTheSameArmSubjectTerm_SucceedsOnceTheFirstIsDeleted()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (subjectId, levelId, sessionId, termId) = await SeedSubjectLevelAndTermAsync();
        var armId = await SeedArmAsync(levelId, sessionId);

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var include = SubjectMappingException.Create(
                Guid.CreateVersion7(), armId, subjectId, termId, SubjectExceptionMode.Include, "Seed reason").Value;
            context.Add(include);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            context.Remove(include);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exclude = SubjectMappingException.Create(
            Guid.CreateVersion7(), armId, subjectId, termId, SubjectExceptionMode.Exclude, "Second reason").Value;
        secondContext.Add(exclude);

        await Should.NotThrowAsync(() => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // AC-2/ApiTestFixture.ReseedSubjectsAsync: a fresh (reset) database carries exactly the 28
    // migration-seeded subjects, none with a code, and no subject_mapping row at all (AC-3 — no
    // session or term is seeded, so nothing could have been mapped).
    [Fact]
    public async Task FreshDatabase_Carries28SeededSubjects_NoCodes_AndNoMappings()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var subjects = await context.Subjects.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        subjects.Count.ShouldBe(28);
        subjects.ShouldAllBe(subject => subject.Code == null);
        subjects.ShouldAllBe(subject => subject.Status == SubjectStatus.Active);
        subjects.Select(subject => subject.Name).ShouldBe(
            SeededSubjects.All.Select(definition => definition.Name).ToArray(), ignoreOrder: true);

        var mappingCount = await context.SubjectMappings.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);
        mappingCount.ShouldBe(0);
    }

    private async Task<(Guid SubjectId, Guid LevelId, Guid SessionId, Guid TermId)> SeedSubjectLevelAndTermAsync()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken)).Id;

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}", new DateOnly(startYear, 9, 1), new DateOnly(startYear + 1, 7, 31)).Value;
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term", new DateOnly(startYear, 9, 1), new DateOnly(startYear, 12, 1)).Value;
        context.Add(term);

        var subject = Subject.Create(Guid.CreateVersion7(), $"Test Subject {startYear}", null, null).Value;
        context.Add(subject);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (subject.Id, levelId, session.Id, term.Id);
    }

    private async Task<Guid> SeedArmAsync(Guid levelId, Guid sessionId)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, "A", null, null).Value;
        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }

    private static int _nextSessionStartYear = 4000;
}
