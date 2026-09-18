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
/// Tests <see cref="CopySubjectMappingsHandler"/>'s own handler-level failure — the closed-
/// destination-term 409 (TASK-0070 dispatch 4, review gap 2). <see cref="SubjectMappingGridDiffTests"/>
/// already covers the additive-only diff computation itself; this proves the term-state guard that
/// runs BEFORE that diff is ever computed, which the shared diff tests cannot reach because they
/// never construct a <see cref="CopySubjectMappingsHandler"/>.
/// </summary>
public sealed class CopySubjectMappingsHandlerTests
{
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly IClassLevelRepository _levels = Substitute.For<IClassLevelRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private readonly Guid _sessionId = Guid.CreateVersion7();
    private readonly Guid _sourceTermId = Guid.CreateVersion7();
    private readonly Guid _destinationTermId = Guid.CreateVersion7();

    public CopySubjectMappingsHandlerTests() => _currentUser.UserId.Returns("admin-1");

    private CopySubjectMappingsHandler CreateHandler() =>
        new(_terms, _sessions, _levels, _subjects, _mappings, _currentUser, _auditSink);

    // Spec 6.6.6, verbatim shape: "Second Term 2026/2027 is closed. Its subject mappings cannot be
    // changed." — the same SubjectMappingMessages.TermClosed builder the grid save and prefill
    // handlers use, pinned here too since copy is its own call site.
    [Fact]
    public async Task HandleAsync_WhenTheDestinationTermIsClosed_IsRejected()
    {
        var sourceTerm = Term.Create(
            _sourceTermId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(_sourceTermId, Arg.Any<CancellationToken>()).Returns(sourceTerm);

        var destinationTerm = Term.Create(
            _destinationTermId, _sessionId, 2, "Second Term", new DateOnly(2027, 1, 1), new DateOnly(2027, 4, 1)).Value;
        destinationTerm.Open();
        destinationTerm.SetTimesSchoolOpened(60);
        destinationTerm.Close(DateTimeOffset.UtcNow, "admin-1");
        _terms.FindTrackedByIdAsync(_destinationTermId, Arg.Any<CancellationToken>()).Returns(destinationTerm);

        var session = AcademicSession.Create(_sessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _sessions.FindReadOnlyByIdAsync(_sessionId, Arg.Any<CancellationToken>()).Returns(session);

        var command = new CopySubjectMappingsCommand(_sourceTermId.ToString(), _destinationTermId.ToString(), DryRun: false);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.term_closed");
        result.Error.Description.ShouldContain("Second Term");
        result.Error.Description.ShouldContain("2026/2027");
        result.Error.Description.ShouldContain("Its subject mappings cannot be changed.");
    }

    // The closed check runs on the DESTINATION only — a closed SOURCE term must not block a copy
    // (its mappings are historical and still readable), proving the guard is keyed on the right term.
    [Fact]
    public async Task HandleAsync_WhenOnlyTheSourceTermIsClosed_IsNotRejectedOnThatGround()
    {
        var closedSourceTerm = Term.Create(
            _sourceTermId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        closedSourceTerm.Open();
        closedSourceTerm.SetTimesSchoolOpened(60);
        closedSourceTerm.Close(DateTimeOffset.UtcNow, "admin-1");
        _terms.FindReadOnlyByIdAsync(_sourceTermId, Arg.Any<CancellationToken>()).Returns(closedSourceTerm);

        var destinationTerm = Term.Create(
            _destinationTermId, _sessionId, 2, "Second Term", new DateOnly(2027, 1, 1), new DateOnly(2027, 4, 1)).Value;
        _terms.FindTrackedByIdAsync(_destinationTermId, Arg.Any<CancellationToken>()).Returns(destinationTerm);

        _levels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ClassLevel>)[]);
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Subject>)[]);
        _mappings.ListActiveByTermReadOnlyAsync(_sourceTermId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[]);
        _mappings.ListByTermTrackedAsync(_destinationTermId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[]);

        var command = new CopySubjectMappingsCommand(_sourceTermId.ToString(), _destinationTermId.ToString(), DryRun: true);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeFalse();
    }
}
