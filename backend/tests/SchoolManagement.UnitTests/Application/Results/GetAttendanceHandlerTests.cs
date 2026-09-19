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

/// <summary>Tests <see cref="GetAttendanceHandler"/>: the derived <c>timesAbsent</c> (ruling A) and its null cases.</summary>
public sealed class GetAttendanceHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly IAttendanceEntryRepository _attendanceEntries = Substitute.For<IAttendanceEntryRepository>();

    public GetAttendanceHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
    }

    private GetAttendanceHandler CreateHandler() => new(_arms, _terms, _enrolments, _resultSets, _attendanceEntries);

    [Fact]
    public async Task HandleAsync_WithNoEntryAndTimesSchoolOpenedBlank_TimesAbsentIsNull()
    {
        var result = await CreateHandler().HandleAsync(
            new GetAttendanceQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TimesSchoolOpened.ShouldBeNull();
        var row = result.Value.Rows.Single();
        row.TimesPresent.ShouldBeNull();
        row.TimesAbsent.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithTimesPresentButTimesSchoolOpenedBlank_TimesAbsentIsNull()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _attendanceEntries.ListReadOnlyAsync(resultSet.Id, Arg.Any<CancellationToken>())
            .Returns([new AttendanceEntrySnapshot(PupilId, 40)]);

        var result = await CreateHandler().HandleAsync(
            new GetAttendanceQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var row = result.Value.Rows.Single();
        row.TimesPresent.ShouldBe(40);
        row.TimesAbsent.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithBothValuesKnown_TimesAbsentIsDerived()
    {
        var termWithOpened = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        termWithOpened.SetTimesSchoolOpened(58);
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(termWithOpened);

        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _attendanceEntries.ListReadOnlyAsync(resultSet.Id, Arg.Any<CancellationToken>())
            .Returns([new AttendanceEntrySnapshot(PupilId, 54)]);

        var result = await CreateHandler().HandleAsync(
            new GetAttendanceQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TimesSchoolOpened.ShouldBe(58);
        var row = result.Value.Rows.Single();
        row.TimesPresent.ShouldBe(54);
        row.TimesAbsent.ShouldBe(4);
    }
}
