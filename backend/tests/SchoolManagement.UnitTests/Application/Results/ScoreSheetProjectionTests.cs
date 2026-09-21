using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="ScoreSheetProjection"/> — THE one place both <c>GetScoreSheetHandler</c> and
/// <c>SaveScoreSheetHandler</c> assemble the sheet (TASK-0090, AC: "returnReason appears on every
/// sheet's resultSet block. One test covers a GET after a return.").
/// </summary>
public sealed class ScoreSheetProjectionTests
{
    [Fact]
    public void Build_OnAReturnedForCorrectionSet_CarriesTheReturnReasonOnTheResultSetBlock()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.ReturnedForCorrection);
        typeof(ResultSet).GetProperty(nameof(ResultSet.ReturnReason))!.SetValue(resultSet, "Please recheck Mathematics marks.");

        var examComponent = AssessmentComponent.Create(
            Guid.CreateVersion7(), "Examination", "Exam", 60, isExamination: true, displayOrder: 1);

        var dto = ScoreSheetProjection.Build(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            resultSet,
            roster: [],
            structure: [examComponent],
            scoresByPupil: new Dictionary<Guid, ScoreSheetVersionRow>());

        dto.ResultSet.ShouldNotBeNull();
        dto.ResultSet!.State.ShouldBe(ResultSetState.ReturnedForCorrection);
        dto.ResultSet!.ReturnReason.ShouldBe("Please recheck Mathematics marks.");
    }

    [Fact]
    public void Build_WithNoResultSet_ReturnsNull()
    {
        var examComponent = AssessmentComponent.Create(
            Guid.CreateVersion7(), "Examination", "Exam", 60, isExamination: true, displayOrder: 1);

        var dto = ScoreSheetProjection.Build(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            resultSet: null,
            roster: [],
            structure: [examComponent],
            scoresByPupil: new Dictionary<Guid, ScoreSheetVersionRow>());

        dto.ResultSet.ShouldBeNull();
    }
}
