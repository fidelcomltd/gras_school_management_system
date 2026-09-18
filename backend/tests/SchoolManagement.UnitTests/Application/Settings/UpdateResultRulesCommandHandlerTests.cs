using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="UpdateResultRulesCommandHandler"/>: the 6.2.9 conditional reason gate, core-subject
/// id resolution, and — the card's central risk — the two INDEPENDENT 6.2.10 hard locks: position
/// scope/tie-break once anything is published in the session, annual method/weights once Third Term is
/// published for any arm, and that resubmitting a locked field's CURRENT value is never refused.
/// </summary>
public sealed class UpdateResultRulesCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();
    private readonly IAssessmentComponentRepository _assessmentComponentRepository = Substitute.For<IAssessmentComponentRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    public UpdateResultRulesCommandHandlerTests()
    {
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingBand>());
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AssessmentComponent>());
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());
        CreateTrackedProfile();
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
    }

    private UpdateResultRulesCommandHandler CreateHandler() => new(
        _resultRulesRepository,
        _schoolProfileRepository,
        _gradingBandRepository,
        _assessmentComponentRepository,
        _configVersionRepository,
        _academicSessionRepository,
        _publishedResultsGate,
        _subjectRepository,
        _ratingScaleRepository,
        _currentUser,
        _auditSink,
        _timeProvider);

    private ResultRules CreateTrackedDefaultRow()
    {
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());
        _resultRulesRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(resultRules);
        return resultRules;
    }

    private SchoolProfile CreateTrackedProfile(int resultRulesVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), resultRulesVersionNumber: resultRulesVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    private static UpdateResultRulesCommand DefaultsCommand(
        AnnualMethod annualMethod = AnnualMethod.SimpleAverage,
        int? weightFirst = null,
        int? weightSecond = null,
        int? weightThird = null,
        PrimaryPositionScope primaryPositionScope = PrimaryPositionScope.Arm,
        bool showLevelPosition = true,
        TieBreakRule tieBreakRule = TieBreakRule.SharedPosition,
        int passMark = 40,
        int promotionThreshold = 40,
        bool requireCorePass = true,
        IReadOnlyList<Guid>? coreSubjectIds = null,
        int minSubjectsForPosition = 1,
        int expectedVersion = 0,
        string? reason = null) =>
        new(
            annualMethod,
            weightFirst,
            weightSecond,
            weightThird,
            primaryPositionScope,
            showLevelPosition,
            tieBreakRule,
            passMark,
            promotionThreshold,
            requireCorePass,
            coreSubjectIds ?? [],
            minSubjectsForPosition,
            expectedVersion,
            reason);

    private static AcademicSession StubActiveSession() =>
        AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;

    [Fact]
    public async Task HandleAsync_WithNoActiveSessionAndValidInput_ReplacesEveryFieldAndWritesAResultRulesVersion()
    {
        var resultRules = CreateTrackedDefaultRow();
        var profile = CreateTrackedProfile(resultRulesVersionNumber: 4);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(
                annualMethod: AnnualMethod.Weighted,
                weightFirst: 30,
                weightSecond: 30,
                weightThird: 40,
                primaryPositionScope: PrimaryPositionScope.Level,
                showLevelPosition: false,
                tieBreakRule: TieBreakRule.ExamThenCa,
                passMark: 45,
                promotionThreshold: 50,
                requireCorePass: false,
                minSubjectsForPosition: 3,
                expectedVersion: 4),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AnnualMethod.ShouldBe(AnnualMethod.Weighted);
        result.Value.WeightFirst.ShouldBe(30);
        result.Value.PrimaryPositionScope.ShouldBe(PrimaryPositionScope.Level);
        result.Value.PassMark.ShouldBe(45);
        result.Value.VersionNumber.ShouldBe(5);
        resultRules.MinSubjectsForPosition.ShouldBe(3);
        profile.ResultRulesVersionNumber.ShouldBe(5);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.ChangedGroup == ConfigVersionGroup.ResultRules && version.Reason == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409AndWritesNoVersionRow()
    {
        CreateTrackedDefaultRow();
        var profile = CreateTrackedProfile(resultRulesVersionNumber: 3);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(expectedVersion: 2),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateResultRulesCommandHandler.StaleVersionErrorCode);
        profile.ResultRulesVersionNumber.ShouldBe(3); // Unchanged — the loser never mutates the row.
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedInTheActiveSessionAndNoReasonIsGiven_ReturnsAValidationFailure()
    {
        CreateTrackedDefaultRow();

        var session = StubActiveSession();
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(2);

        var result = await CreateHandler().HandleAsync(DefaultsCommand(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PublishedResultsReasonGate.ReasonRequiredErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithACoreSubjectIdThatDoesNotExist_ReturnsAValidationFailure()
    {
        CreateTrackedDefaultRow();
        _subjectRepository.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[]);

        var unknownSubjectId = Guid.CreateVersion7();

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(requireCorePass: true, coreSubjectIds: [unknownSubjectId]),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateResultRulesCommandHandler.UnknownSubjectIdErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithACoreSubjectIdThatExistsButIsInactive_ReturnsAValidationFailure()
    {
        CreateTrackedDefaultRow();

        var inactiveSubject = Subject.Create(Guid.CreateVersion7(), "Inactive Subject", null, null).Value;
        inactiveSubject.Deactivate();
        _subjectRepository.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[inactiveSubject]);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(requireCorePass: true, coreSubjectIds: [inactiveSubject.Id]),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateResultRulesCommandHandler.UnknownSubjectIdErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithACoreSubjectIdThatExistsAndIsActive_Succeeds()
    {
        CreateTrackedDefaultRow();

        var activeSubject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;
        _subjectRepository.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[activeSubject]);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(requireCorePass: true, coreSubjectIds: [activeSubject.Id]),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CoreSubjectIds.ShouldBe([activeSubject.Id]);
    }

    [Theory]
    [InlineData(PrimaryPositionScope.Level, TieBreakRule.SharedPosition)]
    [InlineData(PrimaryPositionScope.Arm, TieBreakRule.ExamThenCa)]
    public async Task HandleAsync_WhenScopeOrTieBreakChangesAndSomethingIsPublished_Returns409(
        PrimaryPositionScope submittedScope, TieBreakRule submittedTieBreak)
    {
        CreateTrackedDefaultRow(); // seeded defaults: Arm / SharedPosition

        var session = StubActiveSession();
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(primaryPositionScope: submittedScope, tieBreakRule: submittedTieBreak, reason: "Policy change requested by the head."),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateResultRulesCommandHandler.LockedErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenScopeAndTieBreakAreResubmittedUnchangedWhileSomethingIsPublished_Succeeds()
    {
        CreateTrackedDefaultRow(); // seeded defaults: Arm / SharedPosition

        var session = StubActiveSession();
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(primaryPositionScope: PrimaryPositionScope.Arm, tieBreakRule: TieBreakRule.SharedPosition, reason: "Policy change requested by the head."),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenAnnualMethodChangesAndThirdTermIsPublished_Returns409()
    {
        CreateTrackedDefaultRow(); // seeded default: SimpleAverage

        var session = StubActiveSession();
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.AnyThirdTermPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(annualMethod: AnnualMethod.Weighted, weightFirst: 30, weightSecond: 30, weightThird: 40, reason: "Policy change requested by the head."),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateResultRulesCommandHandler.LockedErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAnnualMethodIsResubmittedUnchangedWhileThirdTermIsPublished_Succeeds()
    {
        CreateTrackedDefaultRow(); // seeded default: SimpleAverage, weights null

        var session = StubActiveSession();
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.AnyThirdTermPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(
            DefaultsCommand(annualMethod: AnnualMethod.SimpleAverage, reason: "Policy change requested by the head."),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }
}
