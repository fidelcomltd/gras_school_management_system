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
/// Tests <see cref="PrefillSubjectMappingsHandler"/> — TASK-0070 delta amendment 5. Additive only,
/// never duplicates, and writes <c>display_order</c> from the per-section SHEET order (not the seed
/// table's grouped order).
/// </summary>
public sealed class PrefillSubjectMappingsHandlerTests
{
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly IClassLevelRepository _levels = Substitute.For<IClassLevelRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private readonly Guid _sessionId = Guid.CreateVersion7();
    private readonly Guid _termId = Guid.CreateVersion7();
    private readonly Guid _nurseryLevelId = Guid.CreateVersion7();

    // Only the real seeded subjects appear on the sheet-order lists, so the fakes reuse the real ids.
    private readonly IReadOnlyList<Subject> _allSubjects = SeededSubjects.All
        .Select(definition => Subject.Create(definition.Id, definition.Name, null, null).Value)
        .ToArray();

    public PrefillSubjectMappingsHandlerTests()
    {
        var term = Term.Create(_termId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindTrackedByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(term);

        var nurseryLevel = ClassLevel.Create(_nurseryLevelId, "Pre-Nursery", SeededClassLevels.NurserySectionId, 1, null).Value;
        _levels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<ClassLevel>)[nurseryLevel]);

        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns(_allSubjects);
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[]);
        _currentUser.UserId.Returns("admin-1");
    }

    private PrefillSubjectMappingsHandler CreateHandler() =>
        new(_terms, _sessions, _levels, _subjects, _mappings, _currentUser, _auditSink);

    private Subject SubjectNamed(string name) => _allSubjects.Single(subject => subject.Name == name);

    [Fact]
    public async Task HandleAsync_Endings_AreAlwaysEmpty()
    {
        var result = await CreateHandler().HandleAsync(new PrefillSubjectMappingsCommand(_termId.ToString(), DryRun: false), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Endings.ShouldBeEmpty();
    }

    // §6.6.9 amendment 5: display_order is written from SeededSubjects.NurserySheetOrder's own
    // position, 1-based — "Number work" is index 0 in that list, so display_order 1.
    [Fact]
    public async Task HandleAsync_WritesDisplayOrderFromTheNurserySheetOrder_NotTheSeedTablesGroupedOrder()
    {
        var result = await CreateHandler().HandleAsync(new PrefillSubjectMappingsCommand(_termId.ToString(), DryRun: false), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        var numberWork = SubjectNamed("Number work");
        var quantitativeReasoning = SubjectNamed("Quantitative Reasoning");
        var rhyme = SubjectNamed("Rhyme");

        var additions = result.Value.Additions;
        additions.Single(a => a.SubjectId == numberWork.Id.ToString()).ClassLevelId.ShouldBe(_nurseryLevelId.ToString());

        // Cross-check display_order against the actually-persisted mapping, since the response DTO
        // (SubjectMappingChangeDto) does not itself carry display_order.
        await _mappings.Received(1).AddAsync(
            Arg.Is<SubjectMapping>(m => m != null && m.SubjectId == numberWork.Id && m.DisplayOrder == 1), Arg.Any<CancellationToken>());
        await _mappings.Received(1).AddAsync(
            Arg.Is<SubjectMapping>(m => m != null && m.SubjectId == quantitativeReasoning.Id && m.DisplayOrder == 4), Arg.Any<CancellationToken>());
        await _mappings.Received(1).AddAsync(
            Arg.Is<SubjectMapping>(m => m != null && m.SubjectId == rhyme.Id && m.DisplayOrder == 14), Arg.Any<CancellationToken>());
    }

    // "Prefilling a term that already holds some of the mappings adds only the missing ones."
    [Fact]
    public async Task HandleAsync_AgainstATermThatAlreadyHoldsSomeMappings_AddsOnlyTheMissingOnes()
    {
        var numberWork = SubjectNamed("Number work");
        var existing = SubjectMapping.Create(Guid.CreateVersion7(), numberWork.Id, _nurseryLevelId, _sessionId, _termId, 1).Value;
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[existing]);

        var result = await CreateHandler().HandleAsync(new PrefillSubjectMappingsCommand(_termId.ToString(), DryRun: false), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Additions.ShouldNotContain(a => a.SubjectId == numberWork.Id.ToString());
        result.Value.Additions.Count.ShouldBe(SeededSubjects.NurserySheetOrder.Count - 1);
        await _mappings.DidNotReceive().AddAsync(
            Arg.Is<SubjectMapping>(m => m != null && m.SubjectId == numberWork.Id), Arg.Any<CancellationToken>());
    }

    // "A second prefill of the same term adds nothing" — every subject already mapped.
    [Fact]
    public async Task HandleAsync_ASecondPrefillOfTheSameTerm_AddsNothing()
    {
        var alreadyMapped = SeededSubjects.NurserySheetOrder
            .Select((name, index) => SubjectMapping.Create(
                Guid.CreateVersion7(), SubjectNamed(name).Id, _nurseryLevelId, _sessionId, _termId, index + 1).Value)
            .ToArray();
        _mappings.ListByTermTrackedAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)alreadyMapped);

        var result = await CreateHandler().HandleAsync(new PrefillSubjectMappingsCommand(_termId.ToString(), DryRun: false), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Additions.ShouldBeEmpty();
        await _mappings.DidNotReceive().AddAsync(Arg.Any<SubjectMapping>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DryRun_ComputesThePreviewButWritesNothing()
    {
        var result = await CreateHandler().HandleAsync(new PrefillSubjectMappingsCommand(_termId.ToString(), DryRun: true), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DryRun.ShouldBeTrue();
        result.Value.Additions.ShouldNotBeEmpty();
        await _mappings.DidNotReceive().AddAsync(Arg.Any<SubjectMapping>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OnAClosedTerm_IsRejectedOutright_EvenUnderDryRun()
    {
        var closedTerm = Term.Create(_termId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        closedTerm.Open();
        closedTerm.SetTimesSchoolOpened(60);
        closedTerm.Close(DateTimeOffset.UtcNow, "admin-1");
        _terms.FindTrackedByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(closedTerm);

        var session = AcademicSession.Create(_sessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _sessions.FindReadOnlyByIdAsync(_sessionId, Arg.Any<CancellationToken>()).Returns(session);

        var result = await CreateHandler().HandleAsync(new PrefillSubjectMappingsCommand(_termId.ToString(), DryRun: true), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.term_closed");
    }
}
