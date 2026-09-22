using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="SaveScoreSheetHandler"/>'s result-set-state gate (TASK-0090 AC: "the class
/// teacher's sheet saves work again [after a return]... one unit test pins it for the score sheet" —
/// the handler already accepted <see cref="ResultSetState.ReturnedForCorrection"/> before this card
/// (line 129's <c>State is not (Draft or ReturnedForCorrection)</c>); this pins that it keeps doing so.
/// </summary>
public sealed class SaveScoreSheetHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid SubjectId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ISubjectMappingExceptionRepository _exceptions = Substitute.For<ISubjectMappingExceptionRepository>();
    private readonly IAssessmentComponentRepository _components = Substitute.For<IAssessmentComponentRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly ISubjectScoreRepository _scores = Substitute.For<ISubjectScoreRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public SaveScoreSheetHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);

        var subject = Subject.Create(SubjectId, "Mathematics", null, null).Value;
        _subjects.FindReadOnlyByIdAsync(SubjectId, Arg.Any<CancellationToken>()).Returns(subject);

        var mapping = SubjectMapping.Create(Guid.CreateVersion7(), SubjectId, ClassLevelId, SessionId, TermId, 1).Value;
        _mappings.ListActiveByLevelAndTermReadOnlyAsync(ClassLevelId, TermId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[mapping]);
        _exceptions.ListByArmAndTermReadOnlyAsync(ArmId, TermId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMappingException>)[]);
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[subject]);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        var examComponent = AssessmentComponent.Create(
            Guid.CreateVersion7(), "Examination", "Exam", 100, isExamination: true, displayOrder: 1);
        _components.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<AssessmentComponent>)[examComponent]);

        _scores.ListActiveTrackedAsync(Arg.Any<Guid>(), SubjectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectScore>)[]);
    }

    private SaveScoreSheetHandler CreateHandler() => new(
        _arms, _sessions, _terms, _subjects,
        new SubjectsInEffectResolver(_arms, _subjects, _mappings, _exceptions),
        _components, _enrolments, _resultSets, _scores, _currentUser, _auditSink);

    private static SaveScoreSheetCommand Command(params SaveScoreSheetRowInput[] rows) =>
        new(ArmId.ToString(), SubjectId.ToString(), TermId.ToString(), null, rows);

    // AC: "They already allow Returned; one unit test pins it for the score sheet."
    [Fact]
    public async Task HandleAsync_OnAReturnedForCorrectionSet_Succeeds()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.ReturnedForCorrection);
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _scores.ListActiveTrackedAsync(resultSet.Id, SubjectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectScore>)[]);

        var row = new SaveScoreSheetRowInput(
            PupilId.ToString(), new Dictionary<string, int?>(StringComparer.Ordinal), ExamMark: 60, ExamAbsent: false);

        var result = await CreateHandler().HandleAsync(Command(row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResultSet.ShouldNotBeNull();
        result.Value.ResultSet!.State.ShouldBe(ResultSetState.ReturnedForCorrection);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResultSetIsApproved_Returns409Locked()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        _resultSets.FindTrackedByArmTermForUpdateAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _scores.ListActiveTrackedAsync(resultSet.Id, SubjectId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectScore>)[]);

        var row = new SaveScoreSheetRowInput(
            PupilId.ToString(), new Dictionary<string, int?>(StringComparer.Ordinal), ExamMark: 60, ExamAbsent: false);

        var result = await CreateHandler().HandleAsync(Command(row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("score_sheet.result_set_locked");
    }
}
