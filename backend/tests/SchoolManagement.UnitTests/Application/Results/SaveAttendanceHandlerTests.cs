using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="SaveAttendanceHandler"/>: every error code, the omitted-vs-null row semantics
/// (Q1-A generalised to a single-value field), and that an existing result set is never flagged
/// <c>needsRecompute</c> by an attendance-only save.
/// </summary>
public sealed class SaveAttendanceHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly IAttendanceEntryRepository _attendanceEntries = Substitute.For<IAttendanceEntryRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public SaveAttendanceHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((SchoolManagement.Domain.Sessions.AcademicSession?)null);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
        _attendanceEntries.ListTrackedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private SaveAttendanceHandler CreateHandler() => new(
        _arms, _sessions, _terms, _enrolments, _resultSets, _attendanceEntries, _currentUser, _auditSink);

    private static SaveAttendanceCommand Command(string? version, params SaveAttendanceRowInput[] rows) =>
        new(ArmId.ToString(), TermId.ToString(), version, rows);

    [Fact]
    public async Task HandleAsync_WhenTheSessionIsClosed_Returns409()
    {
        var closedSession = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            SessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        closedSession.Activate();
        closedSession.Close();
        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(closedSession);

        var result = await CreateHandler().HandleAsync(Command(null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("attendance.session_closed");
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
        result.Error.Code.ShouldBe("attendance.term_closed");
    }

    [Fact]
    public async Task HandleAsync_APupilNotOnTheRoster_Returns422()
    {
        var row = new SaveAttendanceRowInput(Guid.CreateVersion7().ToString(), 10);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].PupilId");
    }

    [Fact]
    public async Task HandleAsync_NegativeTimesPresent_Returns422()
    {
        var row = new SaveAttendanceRowInput(PupilId.ToString(), -1);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].TimesPresent");
    }

    [Fact]
    public async Task HandleAsync_TimesPresentAboveTheTermsTimesSchoolOpened_Returns422()
    {
        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        term.SetTimesSchoolOpened(58);
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 59);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].TimesPresent");
    }

    [Fact]
    public async Task HandleAsync_TimesPresentAboveTwoHundredWhileTimesSchoolOpenedBlank_Returns422()
    {
        // Term.MaxTimesSchoolOpened (200) is the ceiling while the term's own value is unset.
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 201);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].TimesPresent");
    }

    [Fact]
    public async Task HandleAsync_FirstEntryForTheArm_CreatesTheResultSetInDraft()
    {
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 58);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResultSet.ShouldNotBeNull();
        result.Value.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        result.Value.ResultSet!.NeedsRecompute.ShouldBeTrue();
        await _resultSets.Received(1).AddAsync(Arg.Any<ResultSet>(), Arg.Any<CancellationToken>());
        await _attendanceEntries.Received(1).AddAsync(Arg.Any<AttendanceEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OmittedPupil_LeavesTheExistingEntryUntouched()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        var otherPupilId = Guid.CreateVersion7();
        var existingEntry = AttendanceEntry.Create(Guid.CreateVersion7(), resultSet.Id, otherPupilId, 30).Value;
        _attendanceEntries.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingEntry]);
        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null), new ArmRosterPupil(otherPupilId, "GRAS/2026/0002", "Bello", "Musa", null)]);

        var currentVersion = AttendanceVersion.Compute([new AttendanceEntrySnapshot(otherPupilId, 30)]);
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 58);

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var otherRow = result.Value.Rows.Single(r => r.PupilId == otherPupilId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        otherRow.TimesPresent.ShouldBe(30);
        await _attendanceEntries.DidNotReceive().RemoveAsync(Arg.Any<AttendanceEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExplicitNullClearsTheExistingEntry()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        var existingEntry = AttendanceEntry.Create(Guid.CreateVersion7(), resultSet.Id, PupilId, 30).Value;
        _attendanceEntries.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingEntry]);

        var currentVersion = AttendanceVersion.Compute([new AttendanceEntrySnapshot(PupilId, 30)]);
        var row = new SaveAttendanceRowInput(PupilId.ToString(), null);

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.Single().TimesPresent.ShouldBeNull();
        await _attendanceEntries.Received(1).RemoveAsync(existingEntry, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _attendanceEntries.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 58);

        var result = await CreateHandler().HandleAsync(Command("a-stale-hash", row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveAttendanceHandler.StaleVersionErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResultSetIsApproved_Returns409Locked()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _attendanceEntries.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 58);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveAttendanceHandler.ResultSetLockedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_OnAnExistingResultSet_NeverFlagsNeedsRecompute()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        resultSet.MarkComputed(null, DateTimeOffset.UtcNow, pupilCount: 1);
        resultSet.NeedsRecompute.ShouldBeFalse();
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _attendanceEntries.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 58);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        resultSet.NeedsRecompute.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_OnASuccessfulSave_RecordsAnAuditEvent()
    {
        var row = new SaveAttendanceRowInput(PupilId.ToString(), 58);

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _auditSink.Received(1).RecordAsync(
            Arg.Is(Privileges.Results.AttendanceEnter), Arg.Is("attendance_entry"), Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>>());
    }
}
