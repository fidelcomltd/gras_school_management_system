using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="SaveClassTeacherRemarksHandler"/>: every error code, the omitted-vs-blank row
/// semantics, appendix C.6's snapshot-only-on-real-change rule, and that the audit trail never
/// carries the remark's TEXT.
/// </summary>
public sealed class SaveClassTeacherRemarksHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid ActorId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly IPupilRemarkRepository _pupilRemarks = Substitute.For<IPupilRemarkRepository>();
    private readonly IAdminAccountRepository _accounts = Substitute.For<IAdminAccountRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public SaveClassTeacherRemarksHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((SchoolManagement.Domain.Sessions.AcademicSession?)null);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
        _pupilRemarks.ListTrackedAsync(Arg.Any<Guid>(), RemarkKind.ClassTeacher, Arg.Any<CancellationToken>()).Returns([]);

        _currentUser.UserId.Returns(ActorId.ToString());
        var actor = AdminAccount.Create(ActorId, "teacher@example.com", "Mrs Adeyemi", "08012345678", "hash").Value;
        _accounts.FindReadOnlyByIdAsync(ActorId, Arg.Any<CancellationToken>()).Returns(actor);
    }

    private SaveClassTeacherRemarksHandler CreateHandler() => new(
        _arms, _sessions, _terms, _enrolments, _resultSets, _pupilRemarks, _accounts, _currentUser, _auditSink, _timeProvider);

    private static SaveClassTeacherRemarksCommand Command(string? version, params SaveRemarkRowInput[] rows) =>
        new(ArmId.ToString(), TermId.ToString(), version, rows);

    [Fact]
    public async Task HandleAsync_WhenNotAuthenticated_ReturnsUnauthenticated()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await CreateHandler().HandleAsync(Command(null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("authentication.required");
    }

    [Fact]
    public async Task HandleAsync_WhenTheTermIsClosed_Returns409()
    {
        var closedTerm = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        closedTerm.SetTimesSchoolOpened(58);
        closedTerm.Open();
        closedTerm.Close(DateTimeOffset.UtcNow, closedBy: null);
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(closedTerm);

        var result = await CreateHandler().HandleAsync(Command(null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("class_teacher_remarks.term_closed");
    }

    [Fact]
    public async Task HandleAsync_APupilNotOnTheRoster_Returns422()
    {
        var row = new SaveRemarkRowInput(Guid.CreateVersion7().ToString(), "Text");

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].PupilId");
    }

    [Fact]
    public async Task HandleAsync_RemarkOverMaxLength_Returns422()
    {
        var row = new SaveRemarkRowInput(PupilId.ToString(), new string('a', PupilRemark.TextMaxLength + 1));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].Remark");
    }

    [Fact]
    public async Task HandleAsync_FirstRemarkForTheArm_CreatesTheResultSetInDraft()
    {
        var row = new SaveRemarkRowInput(PupilId.ToString(), "A diligent pupil.");

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResultSet.ShouldNotBeNull();
        result.Value.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        result.Value.Rows.Single().Remark.ShouldBe("A diligent pupil.");
        result.Value.Rows.Single().WrittenByName.ShouldBe("Mrs Adeyemi");
        await _resultSets.Received(1).AddAsync(Arg.Any<ResultSet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhitespaceOnlyRemark_ClearsAnExistingRow()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        var existing = PupilRemark.Create(
            Guid.CreateVersion7(), resultSet.Id, PupilId, RemarkKind.ClassTeacher, "Old text.", ActorId, "Mrs Adeyemi", DateTimeOffset.UtcNow).Value;
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.ClassTeacher, Arg.Any<CancellationToken>()).Returns([existing]);

        var currentVersion = RemarkVersion.Compute([new PupilRemarkSnapshot(PupilId, "Old text.", "Mrs Adeyemi", DateTimeOffset.UtcNow)]);
        var row = new SaveRemarkRowInput(PupilId.ToString(), "   ");

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.Single().Remark.ShouldBeNull();
        await _pupilRemarks.Received(1).RemoveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ResavingTheSameTextByADifferentAdmin_LeavesTheSnapshotUntouchedAndIsNotAudited()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        var originalWrittenAt = DateTimeOffset.UtcNow.AddDays(-1);
        var existing = PupilRemark.Create(
            Guid.CreateVersion7(), resultSet.Id, PupilId, RemarkKind.ClassTeacher, "Unchanged.", Guid.CreateVersion7(), "Mr Bello", originalWrittenAt).Value;
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.ClassTeacher, Arg.Any<CancellationToken>()).Returns([existing]);

        var currentVersion = RemarkVersion.Compute([new PupilRemarkSnapshot(PupilId, "Unchanged.", "Mr Bello", originalWrittenAt)]);
        var row = new SaveRemarkRowInput(PupilId.ToString(), "Unchanged.");

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.Single().WrittenByName.ShouldBe("Mr Bello");
        result.Value.Rows.Single().WrittenAt.ShouldBe(originalWrittenAt);
        await _auditSink.DidNotReceive().RecordAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.ClassTeacher, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveRemarkRowInput(PupilId.ToString(), "Text");

        var result = await CreateHandler().HandleAsync(Command("a-stale-hash", row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveClassTeacherRemarksHandler.StaleVersionErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResultSetIsApproved_Returns409Locked()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.ClassTeacher, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveRemarkRowInput(PupilId.ToString(), "Text");

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveClassTeacherRemarksHandler.ResultSetLockedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_OnASuccessfulSave_AuditsWithoutTheRemarkText()
    {
        var row = new SaveRemarkRowInput(PupilId.ToString(), "Something private about the pupil.");

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _auditSink.Received(1).RecordAsync(
            Arg.Is(Privileges.Results.RemarkClassTeacher), Arg.Is("pupil_remark"), Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>>(metadata => DoesNotContainTheRemarkText(metadata)),
            Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>());
    }

    // A plain method call, not an inline `is`/null-conditional lambda body: Arg.Is<T> compiles its
    // predicate as an expression tree, which supports neither.
    private static bool DoesNotContainTheRemarkText(IReadOnlyDictionary<string, object?>? metadata) =>
        metadata is not null && !metadata.Values.OfType<string>().Any(value => value.Contains("Something private", StringComparison.Ordinal));
}
