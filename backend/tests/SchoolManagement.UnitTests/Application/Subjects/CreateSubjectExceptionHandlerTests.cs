using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="CreateSubjectExceptionHandler"/>. Two things pinned as ONE unit here:
/// <list type="bullet">
/// <item>the redundancy rule (spec 6.6.4) — the load-bearing assumption
/// <see cref="SubjectsInEffectResolver"/>'s remarks rely on, that a plain set union/difference is
/// safe only because an include can never name an already-mapped subject and an exclude can never
/// name one that is not, both enforced HERE, at creation;</item>
/// <item>AC-4 — an include and an exclude on the same (arm, subject, term) is REJECTED, not
/// order-resolved, via the single active-uniqueness check on the triple regardless of mode.</item>
/// </list>
/// </summary>
public sealed class CreateSubjectExceptionHandlerTests
{
    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly IClassLevelRepository _levels = Substitute.For<IClassLevelRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ISubjectMappingExceptionRepository _exceptions = Substitute.For<ISubjectMappingExceptionRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private readonly Guid _sessionId = Guid.CreateVersion7();
    private readonly Guid _classLevelId = Guid.CreateVersion7();
    private readonly Guid _armId = Guid.CreateVersion7();
    private readonly Guid _termId = Guid.CreateVersion7();
    private readonly Subject _subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;

    public CreateSubjectExceptionHandlerTests()
    {
        var arm = Arm.Create(_armId, _classLevelId, _sessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(_armId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(_termId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(term);

        var session = AcademicSession.Create(_sessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _sessions.FindReadOnlyByIdAsync(_sessionId, Arg.Any<CancellationToken>()).Returns(session);

        _levels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ClassLevel>)[ClassLevel.Create(_classLevelId, "Primary 4", Guid.CreateVersion7(), 1, null).Value]);

        _subjects.FindReadOnlyByIdAsync(_subject.Id, Arg.Any<CancellationToken>()).Returns(_subject);
        _exceptions.ExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _currentUser.UserId.Returns("admin-1");
    }

    private CreateSubjectExceptionHandler CreateHandler() =>
        new(_arms, _levels, _sessions, _terms, _subjects, _mappings, _exceptions, _currentUser, _auditSink);

    private CreateSubjectExceptionCommand CommandFor(SubjectExceptionMode mode) =>
        new(_armId.ToString(), _subject.Id.ToString(), _termId.ToString(), mode, "A stated reason");

    // The load-bearing assumption, end 1: an INCLUDE on a subject already actively mapped is
    // rejected — this is what stops the resolver ever seeing an include that duplicates a mapping.
    [Fact]
    public async Task HandleAsync_IncludeOnAnAlreadyMappedSubject_IsRejectedAsRedundant()
    {
        _mappings.IsActivelyMappedAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Include), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.redundant_include");
    }

    // The load-bearing assumption, end 2: an EXCLUDE on a subject NOT actively mapped is rejected —
    // this is what stops the resolver ever seeing an exclude with nothing to exclude.
    [Fact]
    public async Task HandleAsync_ExcludeOnASubjectNotMapped_IsRejectedAsRedundant()
    {
        _mappings.IsActivelyMappedAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Exclude), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.redundant_exclude");
    }

    // Positive controls for the same rule: the NON-redundant half of each mode must succeed, or a
    // bug rejecting every exception regardless of reason would still pass the two tests above.
    [Fact]
    public async Task HandleAsync_IncludeOnASubjectNotMapped_Succeeds()
    {
        _mappings.IsActivelyMappedAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Include), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Mode.ShouldBe(SubjectExceptionMode.Include);
    }

    [Fact]
    public async Task HandleAsync_ExcludeOnAnAlreadyMappedSubject_Succeeds()
    {
        _mappings.IsActivelyMappedAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Exclude), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Mode.ShouldBe(SubjectExceptionMode.Exclude);
    }

    // AC-4: a SECOND exception on the same (arm, subject, term) is rejected regardless of mode — an
    // include already present blocks a later EXCLUDE attempt on the identical triple, proving the
    // rejection is keyed on the triple alone, not on "same mode as an existing row."
    [Fact]
    public async Task HandleAsync_WhenAnExceptionAlreadyExistsOnTheTriple_TheOppositeModeIsAlsoRejected_NotOrderResolved()
    {
        // An include already exists (so the mapped-check would otherwise ALLOW an exclude) —
        // isActivelyMapped is irrelevant once ExistsAsync says the triple is already taken.
        _mappings.IsActivelyMappedAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>()).Returns(true);
        _exceptions.ExistsAsync(_armId, _subject.Id, _termId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Exclude), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.duplicate");
    }

    [Fact]
    public async Task HandleAsync_WhenAnExceptionAlreadyExistsOnTheTriple_TheSameModeIsAlsoRejected()
    {
        _mappings.IsActivelyMappedAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>()).Returns(false);
        _exceptions.ExistsAsync(_armId, _subject.Id, _termId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Include), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.duplicate");
    }

    [Fact]
    public async Task HandleAsync_WhenTheArmDoesNotExist_ReturnsArmNotFound()
    {
        _arms.FindReadOnlyByIdAsync(_armId, Arg.Any<CancellationToken>()).Returns((Arm?)null);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Include), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.not_found");
    }

    [Fact]
    public async Task HandleAsync_WhenTheTermDoesNotBelongToTheArmsSession_ReturnsTermSessionMismatch()
    {
        var otherSessionTerm = Term.Create(
            _termId, Guid.CreateVersion7(), 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(otherSessionTerm);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Include), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.term_session_mismatch");
    }

    // Spec 6.6.8: "Exception created on an arm in a closed session | Rejected."
    [Fact]
    public async Task HandleAsync_WhenTheArmsSessionIsClosed_IsRejected()
    {
        var closedSession = AcademicSession.Create(_sessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        closedSession.Close();
        _sessions.FindReadOnlyByIdAsync(_sessionId, Arg.Any<CancellationToken>()).Returns(closedSession);

        var result = await CreateHandler().HandleAsync(CommandFor(SubjectExceptionMode.Include), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.arm_session_closed");
    }

    [Fact]
    public async Task HandleAsync_WhenTheSubjectIsInactive_ReturnsSubjectNotFound()
    {
        var inactiveSubject = Subject.Create(Guid.CreateVersion7(), "Inactive Subject", null, null).Value;
        inactiveSubject.Deactivate();
        _subjects.FindReadOnlyByIdAsync(inactiveSubject.Id, Arg.Any<CancellationToken>()).Returns(inactiveSubject);

        var command = new CreateSubjectExceptionCommand(
            _armId.ToString(), inactiveSubject.Id.ToString(), _termId.ToString(), SubjectExceptionMode.Include, "A reason");

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.subject_not_found");
    }
}
