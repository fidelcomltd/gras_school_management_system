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
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="SaveHeadTeacherRemarksHandler"/>: ruling H's WIDER lock window (Draft through
/// Approved, unlike the class-teacher and attendance sheets) and the fill-all action (rows apply
/// first, never overwrites an existing remark).
/// </summary>
public sealed class SaveHeadTeacherRemarksHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid SecondPupilId = Guid.CreateVersion7();
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

    public SaveHeadTeacherRemarksHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((SchoolManagement.Domain.Sessions.AcademicSession?)null);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>()).Returns(
        [
            new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null),
            new ArmRosterPupil(SecondPupilId, "GRAS/2026/0002", "Bello", "Musa", null),
        ]);

        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
        _pupilRemarks.ListTrackedAsync(Arg.Any<Guid>(), RemarkKind.HeadTeacher, Arg.Any<CancellationToken>()).Returns([]);

        _currentUser.UserId.Returns(ActorId.ToString());
        var actor = AdminAccount.Create(ActorId, "head@example.com", "Mr Okonkwo", "08012345678", "hash").Value;
        _accounts.FindReadOnlyByIdAsync(ActorId, Arg.Any<CancellationToken>()).Returns(actor);
    }

    private SaveHeadTeacherRemarksHandler CreateHandler() => new(
        _arms, _sessions, _terms, _enrolments, _resultSets, _pupilRemarks, _accounts, _currentUser, _auditSink, _timeProvider);

    private static SaveHeadTeacherRemarksCommand Command(string? version, IReadOnlyList<SaveRemarkRowInput> rows, string? fillEmpty = null) =>
        new(ArmId.ToString(), TermId.ToString(), version, rows, fillEmpty);

    [Theory]
    [InlineData(ResultSetState.Draft)]
    [InlineData(ResultSetState.ReturnedForCorrection)]
    [InlineData(ResultSetState.AwaitingApproval)]
    [InlineData(ResultSetState.Approved)]
    public async Task HandleAsync_InAnyOfTheFourAllowedStates_Succeeds(ResultSetState state)
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.HeadTeacher, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveRemarkRowInput(PupilId.ToString(), "A pleasure to have in school.");

        var result = await CreateHandler().HandleAsync(Command(null, [row]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ResultSetState.Published)]
    [InlineData(ResultSetState.Withdrawn)]
    public async Task HandleAsync_OncePublishedOrWithdrawn_Returns409Locked(ResultSetState state)
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.HeadTeacher, Arg.Any<CancellationToken>()).Returns([]);
        var row = new SaveRemarkRowInput(PupilId.ToString(), "Text");

        var result = await CreateHandler().HandleAsync(Command(null, [row]), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveHeadTeacherRemarksHandler.ResultSetLockedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_FillEmpty_SetsTextOnlyForPupilsWithNoRemark()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        var existing = PupilRemark.Create(
            Guid.CreateVersion7(), resultSet.Id, PupilId, RemarkKind.HeadTeacher, "Already written.", ActorId, "Mr Okonkwo", DateTimeOffset.UtcNow).Value;
        _pupilRemarks.ListTrackedAsync(resultSet.Id, RemarkKind.HeadTeacher, Arg.Any<CancellationToken>()).Returns([existing]);

        var currentVersion = RemarkVersion.Compute([new PupilRemarkSnapshot(PupilId, "Already written.", "Mr Okonkwo", DateTimeOffset.UtcNow)]);

        var result = await CreateHandler().HandleAsync(
            Command(currentVersion, [], fillEmpty: "Keep up the good work."), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var untouchedRow = result.Value.Rows.Single(r => r.PupilId == PupilId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        untouchedRow.Remark.ShouldBe("Already written.");
        var filledRow = result.Value.Rows.Single(r => r.PupilId == SecondPupilId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        filledRow.Remark.ShouldBe("Keep up the good work.");
    }

    [Fact]
    public async Task HandleAsync_RowsApplyBeforeFillEmpty_ARowInTheSamePupilIsNotOverwrittenByTheFill()
    {
        var row = new SaveRemarkRowInput(PupilId.ToString(), "Specific remark from the rows.");

        var result = await CreateHandler().HandleAsync(
            Command(null, [row], fillEmpty: "Generic fill text."), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var pupilRow = result.Value.Rows.Single(r => r.PupilId == PupilId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        pupilRow.Remark.ShouldBe("Specific remark from the rows.");
        var otherRow = result.Value.Rows.Single(r => r.PupilId == SecondPupilId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        otherRow.Remark.ShouldBe("Generic fill text.");
    }
}
