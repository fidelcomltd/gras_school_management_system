using NSubstitute;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>Tests <see cref="GetClassTeacherRemarksHandler"/>.</summary>
public sealed class GetClassTeacherRemarksHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly DateTimeOffset WrittenAt = new(2026, 12, 12, 9, 30, 0, TimeSpan.Zero);

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly IPupilRemarkRepository _pupilRemarks = Substitute.For<IPupilRemarkRepository>();

    public GetClassTeacherRemarksHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
    }

    private GetClassTeacherRemarksHandler CreateHandler() => new(_arms, _terms, _enrolments, _resultSets, _pupilRemarks);

    [Fact]
    public async Task HandleAsync_WithNoRemarksEnteredAtAll_ReturnsEveryActivePupilBlank()
    {
        var result = await CreateHandler().HandleAsync(
            new GetClassTeacherRemarksQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBeNull();
        var row = result.Value.Rows.Single();
        row.Remark.ShouldBeNull();
        row.WrittenByName.ShouldBeNull();
        row.WrittenAt.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithARemark_ReturnsItsSnapshot()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _pupilRemarks.ListReadOnlyAsync(resultSet.Id, RemarkKind.ClassTeacher, Arg.Any<CancellationToken>())
            .Returns([new PupilRemarkSnapshot(PupilId, "A diligent pupil.", "Mrs Adeyemi", WrittenAt)]);

        var result = await CreateHandler().HandleAsync(
            new GetClassTeacherRemarksQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldNotBeNull();
        var row = result.Value.Rows.Single();
        row.Remark.ShouldBe("A diligent pupil.");
        row.WrittenByName.ShouldBe("Mrs Adeyemi");
        row.WrittenAt.ShouldBe(WrittenAt);
    }
}
