using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="SaveSubjectMappingGridHandler"/>: TASK-0070 delta amendment 2's data-dependent
/// privilege split (<c>Subject.Map</c> for additions, <c>Subject.Unmap</c> for endings, both when
/// both), the §6.6.6 mark-check seam's two branches, and the closed-term refusal.
/// </summary>
public sealed class SaveSubjectMappingGridHandlerTests
{
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly IClassLevelRepository _levels = Substitute.For<IClassLevelRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ISubjectMappingMarkLookup _markLookup = Substitute.For<ISubjectMappingMarkLookup>();
    private readonly IEffectivePrivilegeProvider _privileges = Substitute.For<IEffectivePrivilegeProvider>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private readonly Guid _sessionId = Guid.CreateVersion7();
    private readonly Guid _termId = Guid.CreateVersion7();
    private readonly Guid _classLevelId = Guid.CreateVersion7();
    private readonly Subject _subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;
    private readonly ClassLevel _level;

    public SaveSubjectMappingGridHandlerTests()
    {
        _level = ClassLevel.Create(_classLevelId, "Primary 4", Guid.CreateVersion7(), 1, null).Value;

        var term = Term.Create(_termId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindTrackedByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(term);

        _levels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<ClassLevel>)[_level]);
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[_subject]);
        _markLookup.FindArmsWithMarksAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMarkArmSummary>)[]);
        _currentUser.UserId.Returns("admin-1");
    }

    private SaveSubjectMappingGridHandler CreateHandler() =>
        new(_terms, _sessions, _levels, _subjects, _mappings, _markLookup, _privileges, _currentUser, _auditSink);

    private void Grant(params string[] privileges) =>
        _privileges.GetGrantsAsync("admin-1", Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<PrivilegeGrant>)privileges
                .Select(privilege => new PrivilegeGrant(privilege, ScopeType.SchoolWide, new HashSet<Guid>(), null))
                .ToArray());

    private SubjectMapping ActiveMapping(int displayOrder = 1) =>
        SubjectMapping.Create(Guid.CreateVersion7(), _subject.Id, _classLevelId, _sessionId, _termId, displayOrder).Value;

    private SaveSubjectMappingGridCommand AdditionOnlyCommand(bool dryRun = false) => new(
        _termId.ToString(),
        [new SubjectMappingGridEntryInput(_subject.Id.ToString(), _classLevelId.ToString(), 1)],
        dryRun);

    private SaveSubjectMappingGridCommand EndingOnlyCommand(bool dryRun = false) =>
        new(_termId.ToString(), [], dryRun);

    // Priority 7: a Map-only holder cannot end a mapping.
    [Fact]
    public async Task HandleAsync_EndingAMapping_WithOnlyMapPrivilege_IsForbidden()
    {
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[ActiveMapping()]);
        Grant(Privileges.Subject.Map); // Map only — NOT Unmap.

        var result = await CreateHandler().HandleAsync(EndingOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.unmap_denied");
    }

    [Fact]
    public async Task HandleAsync_EndingAMapping_WithUnmapPrivilege_Succeeds()
    {
        var mapping = ActiveMapping();
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[mapping]);
        Grant(Privileges.Subject.Unmap);

        var result = await CreateHandler().HandleAsync(EndingOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        mapping.Status.ShouldBe(SubjectMappingStatus.Ended);
    }

    [Fact]
    public async Task HandleAsync_AddingAMapping_WithOnlyUnmapPrivilege_IsForbidden()
    {
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[]);
        Grant(Privileges.Subject.Unmap); // Unmap only — NOT Map.

        var result = await CreateHandler().HandleAsync(AdditionOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.map_denied");
    }

    [Fact]
    public async Task HandleAsync_AddingAMapping_WithMapPrivilege_Succeeds()
    {
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[]);
        Grant(Privileges.Subject.Map);

        var result = await CreateHandler().HandleAsync(AdditionOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _mappings.Received(1).AddAsync(Arg.Any<SubjectMapping>(), Arg.Any<CancellationToken>());
    }

    // Both when it has both: an addition AND an ending in the same save requires BOTH privileges —
    // holding only one is not enough for either half.
    [Fact]
    public async Task HandleAsync_WithBothAdditionsAndEndings_HoldingOnlyMap_IsForbiddenOnTheEndingHalf()
    {
        var otherSubject = Subject.Create(Guid.CreateVersion7(), "English Language", null, null).Value;
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[_subject, otherSubject]);

        var existingMapping = ActiveMapping(); // for _subject — desired grid omits it, so it becomes an ending
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[existingMapping]);
        Grant(Privileges.Subject.Map);

        var command = new SaveSubjectMappingGridCommand(
            _termId.ToString(),
            [new SubjectMappingGridEntryInput(otherSubject.Id.ToString(), _classLevelId.ToString(), 1)], // addition
            DryRun: false);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.unmap_denied");
    }

    // A dry run is evaluated against the SAME privilege rule as a real save — a caller cannot probe
    // the ending set without holding the privilege to actually perform it.
    [Fact]
    public async Task HandleAsync_DryRun_EvaluatesThePrivilegeCheckIdenticallyToARealSave()
    {
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[ActiveMapping()]);
        Grant(Privileges.Subject.Map); // Map only.

        var result = await CreateHandler().HandleAsync(EndingOnlyCommand(dryRun: true), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.unmap_denied");
        await _mappings.DidNotReceive().AddAsync(Arg.Any<SubjectMapping>(), Arg.Any<CancellationToken>());
    }

    // §6.6.6, branch 1: marks exist for the subject in an arm under the level — the ending is
    // rejected, spec-quoted message.
    [Fact]
    public async Task HandleAsync_EndingAMappingWithRecordedMarks_IsRejected()
    {
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[ActiveMapping()]);
        Grant(Privileges.Subject.Unmap);
        _markLookup.FindArmsWithMarksAsync(_subject.Id, _classLevelId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMarkArmSummary>)[new SubjectMarkArmSummary(Guid.CreateVersion7(), "Primary 4A", 28)]);

        var result = await CreateHandler().HandleAsync(EndingOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.marks_recorded");
        result.Error.Description.ShouldContain("Mathematics");
        result.Error.Description.ShouldContain("Primary 4A (28 pupils)");
    }

    // §6.6.6, branch 2 (the positive control): no marks anywhere — the ending succeeds. Without
    // this, a bug that always rejected an ending for any reason would still pass the test above.
    [Fact]
    public async Task HandleAsync_EndingAMappingWithNoRecordedMarks_Succeeds()
    {
        var mapping = ActiveMapping();
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[mapping]);
        Grant(Privileges.Subject.Unmap);
        // Constructor default already stubs FindArmsWithMarksAsync to return empty.

        var result = await CreateHandler().HandleAsync(EndingOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        mapping.Status.ShouldBe(SubjectMappingStatus.Ended);
    }

    // Spec 6.6.6, verbatim: "First Term 2026/2027 is closed. Its subject mappings cannot be changed."
    [Fact]
    public async Task HandleAsync_OnAClosedTerm_IsRejectedOutright()
    {
        var closedTerm = Term.Create(_termId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        closedTerm.Open();
        closedTerm.SetTimesSchoolOpened(60);
        closedTerm.Close(DateTimeOffset.UtcNow, "admin-1");
        _terms.FindTrackedByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(closedTerm);

        var session = AcademicSession.Create(_sessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _sessions.FindReadOnlyByIdAsync(_sessionId, Arg.Any<CancellationToken>()).Returns(session);

        var result = await CreateHandler().HandleAsync(AdditionOnlyCommand(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.term_closed");
        result.Error.Description.ShouldContain("First Term");
        result.Error.Description.ShouldContain("2026/2027");
        result.Error.Description.ShouldContain("Its subject mappings cannot be changed.");
    }

    // A NEW mapping against an inactive subject is rejected — the flip side of spec 6.6.6's
    // "deactivating stops new mappings only": existing mappings survive (proven in
    // SubjectsInEffectResolverTests), but a fresh one against the same inactive subject cannot be
    // created.
    [Fact]
    public async Task HandleAsync_AddingAMappingForAnInactiveSubject_ReturnsSubjectNotFound()
    {
        var inactiveSubject = Subject.Create(Guid.CreateVersion7(), "Retired Subject", null, null).Value;
        inactiveSubject.Deactivate();
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[inactiveSubject]);
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[]);

        var command = new SaveSubjectMappingGridCommand(
            _termId.ToString(), [new SubjectMappingGridEntryInput(inactiveSubject.Id.ToString(), _classLevelId.ToString(), 1)], DryRun: false);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.subject_not_found");
    }

    // A save with no actual diff (every desired entry already active, nothing removed) never touches
    // the privilege check at all — proven by an empty grant set still succeeding.
    [Fact]
    public async Task HandleAsync_WithNoActualDiff_SucceedsWithoutRequiringAnyPrivilege()
    {
        var mapping = ActiveMapping();
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[mapping]);
        // No Grant() call — grants default to an empty substitute return (null), never consulted.

        var command = new SaveSubjectMappingGridCommand(
            _termId.ToString(), [new SubjectMappingGridEntryInput(_subject.Id.ToString(), _classLevelId.ToString(), 1)], DryRun: false);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }
}
