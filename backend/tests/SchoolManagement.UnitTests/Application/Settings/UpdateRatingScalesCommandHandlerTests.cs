using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="UpdateRatingScalesCommandHandler"/>: optimistic concurrency, the whole-set
/// validation gate, the 6.2.9 conditional reason requirement, the name-matched in-use refusal, and
/// that a winning save replaces every scale and bumps <see cref="SchoolProfile.RatingScalesVersionNumber"/>.
/// </summary>
public sealed class UpdateRatingScalesCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<RatingScaleInput> ValidScales =
    [
        new("Custom", [new("N", "Needs Improvement", 1), new("E", "Excellent", 2)]),
    ];

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();

    private readonly IAssessmentComponentRepository _assessmentComponentRepository =
        Substitute.For<IAssessmentComponentRepository>();

    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();
    private readonly IRatingScaleUsageGate _ratingScaleUsageGate = Substitute.For<IRatingScaleUsageGate>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    public UpdateRatingScalesCommandHandlerTests()
    {
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingBand>());
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AssessmentComponent>());
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(ResultRules.CreateSeed(Guid.CreateVersion7()));
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());
        _ratingScaleUsageGate.IsInUseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    private UpdateRatingScalesCommandHandler CreateHandler() => new(
        _schoolProfileRepository,
        _ratingScaleRepository,
        _gradingBandRepository,
        _assessmentComponentRepository,
        _resultRulesRepository,
        _configVersionRepository,
        _academicSessionRepository,
        _publishedResultsGate,
        _ratingScaleUsageGate,
        _currentUser,
        _auditSink,
        _timeProvider);

    private SchoolProfile CreateTrackedProfile(int ratingScalesVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), ratingScalesVersionNumber: ratingScalesVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409AndWritesNoVersionRow()
    {
        CreateTrackedProfile(ratingScalesVersionNumber: 3);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 2, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRatingScalesCommandHandler.StaleVersionErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
        await _ratingScaleRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnInvalidSet_ReturnsTheFirstRuleFailureAndWritesNothing()
    {
        CreateTrackedProfile();

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(
                [new RatingScaleInput("Too small", [new RatingScalePointInput("E", "Excellent", 1)])],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(RatingScaleRules.PointCountInvalidCode);
        await _ratingScaleRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAValidSetAndNoActiveSession_ReplacesTheScalesAndBumpsTheVersion()
    {
        var profile = CreateTrackedProfile(ratingScalesVersionNumber: 4);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 4, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VersionNumber.ShouldBe(5);
        result.Value.Scales.Count.ShouldBe(1);
        result.Value.Scales[0].Name.ShouldBe("Custom");
        result.Value.Scales[0].Points.Count.ShouldBe(2);
        profile.RatingScalesVersionNumber.ShouldBe(5);

        await _ratingScaleRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IReadOnlyList<RatingScale>>(scales => scales != null && scales.Count == 1),
            Arg.Any<CancellationToken>());
        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.ChangedGroup == ConfigVersionGroup.RatingScales && version.Reason == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RemovingAScaleStillInUse_Returns409AndWritesNothing()
    {
        CreateTrackedProfile();

        var existingScale = RatingScale.Create(
            Guid.CreateVersion7(),
            "Nursery development",
            [RatingScalePoint.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "E", "Excellent", 1)]);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingScale]);
        _ratingScaleUsageGate.IsInUseAsync(existingScale.Id, Arg.Any<CancellationToken>()).Returns(true);

        // ValidScales does not include "Nursery development", so it is being removed.
        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRatingScalesCommandHandler.InUseErrorCode);
        await _ratingScaleRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RemovingAScaleNotInUse_Succeeds()
    {
        CreateTrackedProfile();

        var existingScale = RatingScale.Create(
            Guid.CreateVersion7(),
            "Nursery development",
            [RatingScalePoint.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "E", "Excellent", 1)]);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingScale]);
        _ratingScaleUsageGate.IsInUseAsync(existingScale.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _ratingScaleRepository.Received(1).ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedInTheActiveSessionAndNoReasonIsGiven_ReturnsAValidationFailure()
    {
        CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PublishedResultsReasonGate.ReasonRequiredErrorCode);
        await _ratingScaleRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedAndAReasonIsGiven_SucceedsAndStoresTheReason()
    {
        var profile = CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 0, Reason: "The school added a custom scale."),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        profile.RatingScalesVersionNumber.ShouldBe(1);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.Reason == "The school added a custom scale."),
            Arg.Any<CancellationToken>());
    }
}
