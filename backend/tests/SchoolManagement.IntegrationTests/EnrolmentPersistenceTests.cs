using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0059's two database-enforced guarantees, proven directly against the persistence mechanism
/// rather than through any endpoint — there is no enrolment route yet (out of scope this card).
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class EnrolmentPersistenceTests(ApiTestFixture fixture)
{
    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    // Spec 02 §5.2: "A pupil has exactly one open enrolment at any moment... Enforced by a partial
    // unique index on (pupil_id) where effective_to is null." Proven by attempting a SECOND open
    // row for the same pupil and expecting the database to reject it — a domain-only check could
    // never prove this, because nothing in a single Enrolment instance can see a sibling row.
    [Fact]
    public async Task SecondOpenEnrolmentForTheSamePupil_ViolatesThePartialUniqueIndex()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var pupilId = await SeedPupilAsync();
        var sessionId = await SeedSessionAsync();
        var firstArmId = await SeedArmAsync(sessionId, "1A");
        var secondArmId = await SeedArmAsync(sessionId, "1B");

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = Enrolment.Open(Guid.CreateVersion7(), pupilId, firstArmId, new DateOnly(2026, 9, 14)).Value;
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = Enrolment.Open(Guid.CreateVersion7(), pupilId, secondArmId, new DateOnly(2026, 9, 15)).Value;
        secondContext.Add(second);

        await Should.ThrowAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // The positive control for the same index: once the first row is CLOSED, a second open
    // enrolment for the same pupil is not a duplicate any more and must succeed. Without this, a
    // bug that rejected every second row for ANY reason would still make the test above pass.
    [Fact]
    public async Task SecondEnrolmentForTheSamePupil_SucceedsOnceTheFirstIsClosed()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var pupilId = await SeedPupilAsync();
        var sessionId = await SeedSessionAsync();
        var firstArmId = await SeedArmAsync(sessionId, "2A");
        var secondArmId = await SeedArmAsync(sessionId, "2B");

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = Enrolment.Open(Guid.CreateVersion7(), pupilId, firstArmId, new DateOnly(2026, 9, 14)).Value;
            first.Close(new DateOnly(2026, 10, 1)).IsSuccess.ShouldBeTrue();
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = Enrolment.Open(Guid.CreateVersion7(), pupilId, secondArmId, new DateOnly(2026, 10, 1)).Value;
        secondContext.Add(second);

        await Should.NotThrowAsync(() => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // 02-data-model.md §5.2, near-verbatim: "a pupil row does not carry a level or an arm column...
    // this is the single most important normalisation in the schema." Asserted against the REAL
    // database schema (information_schema), not the C# entity, so a column added straight to a
    // migration without a matching Pupil property would still be caught.
    [Fact]
    public async Task PupilsTable_HasNoArmIdOrClassLevelIdColumn()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var forbiddenColumns = await context.Database
            .SqlQuery<string>($@"
                SELECT column_name AS ""Value""
                FROM information_schema.columns
                WHERE table_name = 'pupils' AND column_name IN ('arm_id', 'class_level_id')")
            .ToListAsync(TestContext.Current.CancellationToken);

        forbiddenColumns.ShouldBeEmpty(
            "spec 02 §5.2: a pupil row must never carry an arm or level column — which arm a " +
            "pupil is in is a query against enrolment for the open row.");
    }

    // Spec 06 §6.4.6's soft capacity limit, and spec 07 §6.5.14's pending-exclusion invariant
    // together: the count is OPEN enrolments only, and a pending pupil (which by construction can
    // never actually acquire one, since admission approval sets Active and opens the enrolment in
    // the same transaction) is defence-in-depth excluded rather than assumed impossible.
    [Fact]
    public async Task CapacityCount_CountsOnlyOpenEnrolments_AndExcludesAPendingPupilsRow()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var sessionId = await SeedSessionAsync();
        var armId = await SeedArmAsync(sessionId, "3A");

        var openActivePupilId = await SeedPupilAsync(PupilStatus.Active);
        var closedActivePupilId = await SeedPupilAsync(PupilStatus.Transferred);
        var pendingPupilId = await SeedPupilAsync(PupilStatus.Pending);

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var open = Enrolment.Open(Guid.CreateVersion7(), openActivePupilId, armId, new DateOnly(2026, 9, 14)).Value;
            context.Add(open);

            var closed = Enrolment.Open(Guid.CreateVersion7(), closedActivePupilId, armId, new DateOnly(2026, 9, 1)).Value;
            closed.Close(new DateOnly(2026, 9, 10)).IsSuccess.ShouldBeTrue();
            context.Add(closed);

            // A pending pupil can never really hold an enrolment (see the test's own remarks) —
            // seeded directly through the DbContext, bypassing that invariant, purely to prove the
            // count's defence-in-depth join actually excludes it rather than merely never meeting it.
            var pendingOpen = Enrolment.Open(Guid.CreateVersion7(), pendingPupilId, armId, new DateOnly(2026, 9, 14)).Value;
            context.Add(pendingOpen);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var countScope = fixture.CreateScope();
        var repository = countScope.ServiceProvider.GetRequiredService<IEnrolmentRepository>();

        var count = await repository.CountOpenExcludingPendingByArmAsync(armId, TestContext.Current.CancellationToken);

        count.ShouldBe(1);
    }

    private static int _nextPupilSuffix;

    private async Task<Guid> SeedPupilAsync(PupilStatus status = PupilStatus.Active)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Letters only (the surname validator rejects digits) — a distinct suffix per call so
        // several seeds within one test never collide, same technique SeedSessionAsync uses with a
        // numeric counter.
        var suffix = ToLetters(Interlocked.Increment(ref _nextPupilSuffix));

        var creation = Pupil.Create(
            Guid.CreateVersion7(),
            $"Surname{suffix}",
            "Chidera",
            middleName: null,
            PupilSex.Female,
            new DateOnly(2018, 1, 1),
            asOfDate: new DateOnly(2026, 9, 9),
            nationality: null,
            "Anambra",
            "Awka South",
            "14 Zik Avenue, Awka",
            previousSchool: null,
            previousClass: null,
            otherInformation: null);

        creation.IsSuccess.ShouldBeTrue(creation.IsFailure ? creation.Error.Description : string.Empty);

        var pupil = creation.Value;
        context.Add(pupil);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (status != PupilStatus.Pending)
        {
            // Pupil exposes no ordinary write path to any status but Pending (TASK-0050's own
            // boundary) — the same accepted direct-SQL technique PupilEndpointsTests uses for its
            // own otherwise-unreachable state.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = {status.ToString()} WHERE id = {pupil.Id}",
                TestContext.Current.CancellationToken);
        }

        return pupil.Id;
    }

    /// <summary>Base-26 letters (A, B, ... Z, AA, AB, ...) for a distinct, validator-safe surname suffix.</summary>
    private static string ToLetters(int value)
    {
        var letters = string.Empty;

        while (value > 0)
        {
            var remainder = (value - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            value = (value - 1) / 26;
        }

        return letters;
    }

    private static int _nextSessionStartYear = 3000;

    private async Task<Guid> SeedSessionAsync()
    {
        await using var scope = fixture.CreateScope();
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
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var levelId = (await context.ClassLevels.AsNoTracking()
            .FirstAsync(TestContext.Current.CancellationToken)).Id;

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, label, null, null).Value;

        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }
}
