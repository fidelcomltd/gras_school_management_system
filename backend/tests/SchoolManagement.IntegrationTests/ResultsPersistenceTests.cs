using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Authorization;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.IntegrationTests.Infrastructure;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// TASK-0076 dispatch A: <c>result_set</c>/<c>subject_score</c> persistence (spec 09 §6.7.3), the
/// arm roster query (§6.7.4), and the four real queries that replace the TASK-0069/TASK-0070 seams
/// now that these tables exist. No score-sheet endpoint exists yet (dispatch B) — everything here is
/// proven directly against the persistence mechanism, the same technique
/// <c>SubjectPersistenceTests</c>/<c>EnrolmentPersistenceTests</c> use.
/// </summary>
[Collection(ApiTestCollectionDefinition.Name)]
public sealed class ResultsPersistenceTests(ApiTestFixture fixture)
{
    private void RequireDatabase()
    {
        if (!fixture.IsDatabaseAvailable)
        {
            Assert.Skip(fixture.SkipReason ?? "No PostgreSQL available for integration tests.");
        }
    }

    // Spec 6.7.3: "Unique together with term_id. One result set per arm per term." Proven by
    // attempting a SECOND result set for the same arm and term — a domain-only check could never
    // prove this, because nothing in a single ResultSet instance can see a sibling row.
    [Fact]
    public async Task SecondResultSet_OnTheSameArmAndTerm_ViolatesTheUniqueIndex()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (_, termId, armId, _, _) = await SeedSessionTermArmSubjectAsync();

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
        secondContext.Add(second);

        await Should.ThrowAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // The positive control for the same index: a DIFFERENT arm in the same term is not a duplicate
    // and must succeed. Without this, a bug that rejected every second row for ANY reason would still
    // make the test above pass.
    [Fact]
    public async Task SecondResultSet_ForADifferentArmInTheSameTerm_Succeeds()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (sessionId, termId, armId, levelId, _) = await SeedSessionTermArmSubjectAsync();

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var secondArmId = await SeedArmAsync(sessionId, levelId, "B");

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = ResultSet.Create(Guid.CreateVersion7(), secondArmId, termId).Value;
        secondContext.Add(second);

        await Should.NotThrowAsync(() => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // Spec 6.7.3: "Unique together with subject_id and term_id" WHERE voided_at IS NULL. Proven by
    // attempting a SECOND non-voided mark for the same pupil/subject/term.
    [Fact]
    public async Task SecondSubjectScore_ForTheSamePupilSubjectTerm_ViolatesThePartialUniqueIndex()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (_, termId, armId, _, subjectId) = await SeedSessionTermArmSubjectAsync();
        var pupilId = await SeedActivePupilInArmAsync(armId, "Adeyemi", new DateOnly(2026, 9, 1));
        var resultSetId = await SeedResultSetAsync(armId, termId, ResultSetState.Draft);

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = SubjectScore.Create(
                Guid.CreateVersion7(), resultSetId, pupilId, subjectId, termId, "{}", examMark: 40, examAbsent: false).Value;
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = SubjectScore.Create(
            Guid.CreateVersion7(), resultSetId, pupilId, subjectId, termId, "{}", examMark: 45, examAbsent: false).Value;
        secondContext.Add(second);

        await Should.ThrowAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // The positive control for the same index: without the "WHERE voided_at IS NULL" filter,
    // re-entering marks after a void would fail forever. Once the first row is VOIDED, a fresh entry
    // for the same pupil/subject/term must succeed.
    [Fact]
    public async Task SecondSubjectScore_SucceedsOnceTheFirstIsVoided()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (_, termId, armId, _, subjectId) = await SeedSessionTermArmSubjectAsync();
        var pupilId = await SeedActivePupilInArmAsync(armId, "Adeyemi", new DateOnly(2026, 9, 1));
        var resultSetId = await SeedResultSetAsync(armId, termId, ResultSetState.Draft);

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = SubjectScore.Create(
                Guid.CreateVersion7(), resultSetId, pupilId, subjectId, termId, "{}", examMark: 40, examAbsent: false).Value;
            first.Void("Entered against the wrong pupil.", "tester", DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
            context.Add(first);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var secondScope = fixture.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = SubjectScore.Create(
            Guid.CreateVersion7(), resultSetId, pupilId, subjectId, termId, "{}", examMark: 45, examAbsent: false).Value;
        secondContext.Add(second);

        await Should.NotThrowAsync(() => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // Spec 6.7.4: "Surname ascending. Fixed... Row order is never affected by marks." Plus the
    // pending-exclusion and active-only rules the card names explicitly. A pupil with a CLOSED
    // enrolment (transferred out) and a pupil directly wired PENDING-with-an-open-enrolment (which
    // cannot happen through the application — admission approval sets Active and opens the enrolment
    // together — seeded directly the same way EnrolmentPersistenceTests proves its own defence in
    // depth) must both be absent.
    [Fact]
    public async Task Roster_ReturnsOnlyActivePupilsWithOpenEnrolment_OrderedSurnameThenId()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (_, _, armId, _, _) = await SeedSessionTermArmSubjectAsync();

        var zebra = await SeedActivePupilInArmAsync(armId, "Zeal", new DateOnly(2026, 9, 1));
        var okaforFirst = await SeedActivePupilInArmAsync(armId, "Okafor", new DateOnly(2026, 9, 1));
        var okaforSecond = await SeedActivePupilInArmAsync(armId, "Okafor", new DateOnly(2026, 9, 1));

        await using (var scope = fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Transferred out: enrolment closed. Must not appear.
            var transferred = Pupil.Create(
                Guid.CreateVersion7(), "Transferred", "Chidera", null, PupilSex.Female,
                new DateOnly(2018, 1, 1), new DateOnly(2026, 9, 9), null,
                "Anambra", "Awka South", "14 Zik Avenue, Awka", null, null, null).Value;
            context.Add(transferred);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE pupils SET status = 'Active' WHERE id = {transferred.Id}", TestContext.Current.CancellationToken);
            var closedEnrolment = Enrolment.Open(Guid.CreateVersion7(), transferred.Id, armId, new DateOnly(2026, 9, 1)).Value;
            closedEnrolment.Close(new DateOnly(2026, 10, 1)).IsSuccess.ShouldBeTrue();
            context.Add(closedEnrolment);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Directly wired Pending-with-an-open-enrolment: defence-in-depth proof. Must not appear.
            var pending = Pupil.Create(
                Guid.CreateVersion7(), "Pending", "Chidera", null, PupilSex.Female,
                new DateOnly(2018, 1, 1), new DateOnly(2026, 9, 9), null,
                "Anambra", "Awka South", "14 Zik Avenue, Awka", null, null, null).Value;
            context.Add(pending);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var pendingEnrolment = Enrolment.Open(Guid.CreateVersion7(), pending.Id, armId, new DateOnly(2026, 9, 1)).Value;
            context.Add(pendingEnrolment);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readScope = fixture.CreateScope();
        var repository = readScope.ServiceProvider.GetRequiredService<IEnrolmentRepository>();

        var roster = await repository.ListActiveRosterByArmAsync(armId, TestContext.Current.CancellationToken);

        roster.Select(pupil => pupil.PupilId).ShouldBe([okaforFirst, okaforSecond, zebra]);
    }

    // TASK-0069's honestly-false stand-in always answered false; this would FAIL against it. Also
    // proves a mark in a DIFFERENT session leaves this session's lock unaffected.
    [Fact]
    public async Task SessionLock_ReportsTrue_OnceAMarkExistsAnywhereInTheSession()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (sessionId, termId, armId, _, subjectId) = await SeedSessionTermArmSubjectAsync();
        var pupilId = await SeedActivePupilInArmAsync(armId, "Adeyemi", new DateOnly(2026, 9, 1));

        await using (var scope = fixture.CreateScope())
        {
            var lockLookup = scope.ServiceProvider.GetRequiredService<ISubjectScoreSessionLockLookup>();
            (await lockLookup.AnyScoreExistsInSessionAsync(sessionId, TestContext.Current.CancellationToken))
                .ShouldBeFalse();
        }

        await SeedSubjectScoreAsync(armId, termId, pupilId, subjectId, voided: false);

        await using var scope2 = fixture.CreateScope();
        var lockLookup2 = scope2.ServiceProvider.GetRequiredService<ISubjectScoreSessionLockLookup>();

        (await lockLookup2.AnyScoreExistsInSessionAsync(sessionId, TestContext.Current.CancellationToken))
            .ShouldBeTrue();

        var (otherSessionId, _, _, _, _) = await SeedSessionTermArmSubjectAsync();
        (await lockLookup2.AnyScoreExistsInSessionAsync(otherSessionId, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }

    // TASK-0070's honestly-empty stand-in always answered []; this would FAIL against it. Also
    // proves a VOIDED mark does not count — spec 6.6.6's check is about live marks, not history.
    [Fact]
    public async Task MarkLookup_FindsTheArmAndPupilCount_ExcludingVoidedMarks()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (_, termId, armId, levelId, subjectId) = await SeedSessionTermArmSubjectAsync();
        var pupilA = await SeedActivePupilInArmAsync(armId, "Adeyemi", new DateOnly(2026, 9, 1));
        var pupilB = await SeedActivePupilInArmAsync(armId, "Bello", new DateOnly(2026, 9, 1));

        await SeedSubjectScoreAsync(armId, termId, pupilA, subjectId, voided: false);
        await SeedSubjectScoreAsync(armId, termId, pupilB, subjectId, voided: true);

        await using var scope = fixture.CreateScope();
        var markLookup = scope.ServiceProvider.GetRequiredService<ISubjectMappingMarkLookup>();

        var summaries = await markLookup.FindArmsWithMarksAsync(subjectId, levelId, termId, TestContext.Current.CancellationToken);

        summaries.Count.ShouldBe(1);
        summaries[0].ArmId.ShouldBe(armId);
        summaries[0].PupilCount.ShouldBe(1);
    }

    // TASK-0069's honestly-zero stand-in always answered 0; this would FAIL against it.
    [Fact]
    public async Task PublishedGate_CountsOnlyPublishedResultSetsInTheSession()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (sessionId, termId, armId, _, _) = await SeedSessionTermArmSubjectAsync();
        var resultSetId = await SeedResultSetAsync(armId, termId, ResultSetState.Draft);

        await using (var scope = fixture.CreateScope())
        {
            var gate = scope.ServiceProvider.GetRequiredService<IPublishedResultsGate>();
            (await gate.CountPublishedInSessionAsync(sessionId, TestContext.Current.CancellationToken)).ShouldBe(0);
        }

        await MarkResultSetStateAsync(resultSetId, ResultSetState.Published);

        await using var scope2 = fixture.CreateScope();
        var gate2 = scope2.ServiceProvider.GetRequiredService<IPublishedResultsGate>();
        (await gate2.CountPublishedInSessionAsync(sessionId, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    // TASK-0060, cited by this card's own notes: PrivilegeDecision ignores PrivilegeGrant.SessionId
    // entirely, and result_set is the first SESSION-BEARING ResultSet-scope target with a REAL
    // lookup (previously it threw, so no resolution — real or wrong — could ever come out of it). Not
    // a fix — this documents today's behaviour: a grant scoped to a DIFFERENT session than the result
    // set's own still authorises, because neither ScopeResolver nor PrivilegeDecision ever compares
    // them. No route declares ScopeParameterKind.ResultSet yet (this card's own contract delta scopes
    // score-sheet routes by arm id directly), so this remains unreached by any real caller today.
    [Fact]
    public async Task ScopeResolution_ForAResultSet_IgnoresTheGrantsSessionId_DocumentingTask0060()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (sessionId, termId, armId, _, _) = await SeedSessionTermArmSubjectAsync();
        var resultSetId = await SeedResultSetAsync(armId, termId, ResultSetState.Draft);

        await using var scope = fixture.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IScopeResolver>();

        var resolution = await resolver.ResolveAsync(
            ScopeParameterKind.ResultSet, resultSetId, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.ResolvedArm>().ArmId.ShouldBe(armId);

        // Scoped to a session that is NOT this result set's own session.
        var otherSessionId = Guid.CreateVersion7();
        otherSessionId.ShouldNotBe(sessionId);
        var grant = new PrivilegeGrant(Privileges.Results.View, ScopeType.ArmList, new HashSet<Guid> { armId }, otherSessionId);

        PrivilegeDecision.IsAuthorized([grant], Privileges.Results.View, resolution).ShouldBeTrue();
    }

    // The old NotYetImplementedResultSetArmLookup THREW; this would FAIL against it outright.
    [Fact]
    public async Task ResultSetArmLookup_ResolvesTheArm_AndNullForAnUnknownId()
    {
        RequireDatabase();
        await fixture.ResetDatabaseAsync(TestContext.Current.CancellationToken);

        var (_, termId, armId, _, _) = await SeedSessionTermArmSubjectAsync();
        var resultSetId = await SeedResultSetAsync(armId, termId, ResultSetState.Draft);

        await using var scope = fixture.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IResultSetArmLookup>();

        (await lookup.GetArmIdAsync(resultSetId, TestContext.Current.CancellationToken)).ShouldBe(armId);
        (await lookup.GetArmIdAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static int _nextSessionStartYear = 6000;

    private async Task<(Guid SessionId, Guid TermId, Guid ArmId, Guid LevelId, Guid SubjectId)> SeedSessionTermArmSubjectAsync()
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var startYear = Interlocked.Increment(ref _nextSessionStartYear);
        var session = AcademicSession.Create(
            Guid.CreateVersion7(), $"{startYear}/{startYear + 1}",
            new DateOnly(startYear, 9, 1), new DateOnly(startYear + 1, 7, 31)).Value;
        context.Add(session);

        var term = Term.Create(
            Guid.CreateVersion7(), session.Id, 1, "First Term",
            new DateOnly(startYear, 9, 1), new DateOnly(startYear, 12, 1)).Value;
        context.Add(term);

        var level = await context.ClassLevels.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken);
        var arm = Arm.Create(Guid.CreateVersion7(), level.Id, session.Id, "A", null, null).Value;
        context.Add(arm);

        var subject = Subject.Create(Guid.CreateVersion7(), $"Test Subject {startYear}", null, null).Value;
        context.Add(subject);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (session.Id, term.Id, arm.Id, level.Id, subject.Id);
    }

    private async Task<Guid> SeedArmAsync(Guid sessionId, Guid levelId, string label)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var arm = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, label, null, null).Value;
        context.Add(arm);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return arm.Id;
    }

    private async Task<Guid> SeedActivePupilInArmAsync(Guid armId, string surname, DateOnly effectiveFrom)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pupil = Pupil.Create(
            Guid.CreateVersion7(), surname, "Chidera", null, PupilSex.Female,
            new DateOnly(2018, 1, 1), new DateOnly(2026, 9, 9), null,
            "Anambra", "Awka South", "14 Zik Avenue, Awka", null, null, null).Value;
        context.Add(pupil);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Pupil exposes no ordinary write path to any status but Pending (TASK-0050's own boundary) —
        // the same accepted direct-SQL technique EnrolmentPersistenceTests.SeedPupilAsync uses.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pupils SET status = 'Active' WHERE id = {pupil.Id}", TestContext.Current.CancellationToken);

        var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, armId, effectiveFrom).Value;
        context.Add(enrolment);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return pupil.Id;
    }

    private async Task<Guid> SeedResultSetAsync(Guid armId, Guid termId, ResultSetState state)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resultSet = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
        context.Add(resultSet);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (state != ResultSetState.Draft)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE result_set SET state = {state.ToString()} WHERE id = {resultSet.Id}",
                TestContext.Current.CancellationToken);
        }

        return resultSet.Id;
    }

    private async Task MarkResultSetStateAsync(Guid resultSetId, ResultSetState state)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE result_set SET state = {state.ToString()} WHERE id = {resultSetId}",
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Creates a mark for (<paramref name="pupilId"/>, <paramref name="subjectId"/>,
    /// <paramref name="termId"/>), reusing an existing result set for (<paramref name="armId"/>,
    /// <paramref name="termId"/>) or creating one, so several marks in one test share a result set the
    /// way a real score sheet would.
    /// </summary>
    private async Task<Guid> SeedSubjectScoreAsync(Guid armId, Guid termId, Guid pupilId, Guid subjectId, bool voided)
    {
        await using var scope = fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var resultSet = await context.ResultSets
            .FirstOrDefaultAsync(rs => rs.ArmId == armId && rs.TermId == termId, TestContext.Current.CancellationToken);

        if (resultSet is null)
        {
            resultSet = ResultSet.Create(Guid.CreateVersion7(), armId, termId).Value;
            context.Add(resultSet);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var score = SubjectScore.Create(
            Guid.CreateVersion7(), resultSet.Id, pupilId, subjectId, termId, "{}", examMark: 40, examAbsent: false).Value;

        if (voided)
        {
            score.Void("Entered against the wrong pupil.", "tester", DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        }

        context.Add(score);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return score.Id;
    }
}
