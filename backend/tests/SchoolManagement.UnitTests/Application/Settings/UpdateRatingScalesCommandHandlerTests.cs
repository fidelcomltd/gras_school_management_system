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
/// validation gate, the 6.2.9 conditional reason requirement, the id-matched in-use refusal (stage 1
/// review fix — matching moved from scale NAME to scale ID, since stage 2/3 rating blocks reference a
/// scale by id and a save must never silently mint it a new one), and that a winning save replaces
/// every scale and bumps <see cref="SchoolProfile.RatingScalesVersionNumber"/>.
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
    private readonly IDevelopmentDomainRepository _developmentDomainRepository = Substitute.For<IDevelopmentDomainRepository>();
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
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<DevelopmentDomain>());
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
        _developmentDomainRepository,
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

    [Fact]
    public async Task HandleAsync_ResavingWithoutChanges_KeepsEveryScaleAndPointId()
    {
        CreateTrackedProfile();

        var scaleId = Guid.CreateVersion7();
        var pointId = Guid.CreateVersion7();
        var secondPointId = Guid.CreateVersion7();
        var existingScale = RatingScale.Create(
            scaleId,
            "Custom",
            [
                RatingScalePoint.Create(pointId, scaleId, "N", "Needs Improvement", 1),
                RatingScalePoint.Create(secondPointId, scaleId, "E", "Excellent", 2),
            ]);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingScale]);

        var resubmitted = new RatingScaleInput(
            "Custom",
            [
                new RatingScalePointInput("N", "Needs Improvement", 1, Id: pointId),
                new RatingScalePointInput("E", "Excellent", 2, Id: secondPointId),
            ],
            Id: existingScale.Id);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand([resubmitted], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Scales[0].Id.ShouldBe(existingScale.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Scales[0].Points[0].Id.ShouldBe(pointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        // The usage gate must never be asked about a scale that is still present (by id) in the
        // submitted set — it is not being removed, merely resaved unchanged.
        await _ratingScaleUsageGate.DidNotReceive().IsInUseAsync(existingScale.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RenamingAnExistingScaleById_KeepsItsIdAndNeverConsultsTheUsageGate()
    {
        CreateTrackedProfile();

        var existingScale = RatingScale.Create(
            Guid.CreateVersion7(),
            "Old name",
            [RatingScalePoint.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "N", "Needs Improvement", 1),
             RatingScalePoint.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "E", "Excellent", 2)]);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingScale]);

        var renamed = new RatingScaleInput(
            "New name",
            existingScale.Points.Select(point => new RatingScalePointInput(point.PointCode, point.PointLabel, point.PointOrder, Id: point.Id)).ToList(),
            Id: existingScale.Id);

        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand([renamed], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Scales[0].Id.ShouldBe(existingScale.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Scales[0].Name.ShouldBe("New name");
        await _ratingScaleUsageGate.DidNotReceive().IsInUseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ANewScaleSharingAnExistingScalesName_StillTreatsTheExistingOneAsRemovedById()
    {
        // The load-bearing regression this stage-1 fix closes: matching used to go by NAME, so a
        // same-named replacement read as an update. It must now read as remove-old/add-new by id.
        CreateTrackedProfile();

        var existingScale = RatingScale.Create(
            Guid.CreateVersion7(),
            "Custom",
            [RatingScalePoint.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "N", "Needs Improvement", 1),
             RatingScalePoint.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "E", "Excellent", 2)]);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingScale]);
        _ratingScaleUsageGate.IsInUseAsync(existingScale.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Same name, no id — a genuinely new scale on the wire, not a rename.
        var result = await CreateHandler().HandleAsync(
            new UpdateRatingScalesCommand(ValidScales, ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Scales[0].Id.ShouldNotBe(existingScale.Id.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        await _ratingScaleUsageGate.Received(1).IsInUseAsync(existingScale.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownScaleId_Returns422NamingTheScaleIndex()
    {
        CreateTrackedProfile();
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());

        var unknownId = Guid.CreateVersion7();
        var command = new UpdateRatingScalesCommand(
            [
                new RatingScaleInput(
                    "Custom",
                    [new RatingScalePointInput("N", "Needs Improvement", 1), new RatingScalePointInput("E", "Excellent", 2)],
                    Id: unknownId),
            ],
            ExpectedVersion: 0,
            Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRatingScalesCommandHandler.UnknownScaleIdErrorCode);
        var error = result.Error.ShouldBeOfType<RatingScaleValidationError>();
        error.ScaleIndex.ShouldBe(0);
        await _ratingScaleRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownPointIdOnAnExistingScale_Returns422NamingScaleAndPointIndex()
    {
        CreateTrackedProfile();

        var scaleId = Guid.CreateVersion7();
        var knownPointId = Guid.CreateVersion7();
        var existingScale = RatingScale.Create(
            scaleId,
            "Custom",
            [RatingScalePoint.Create(knownPointId, scaleId, "N", "Needs Improvement", 1)]);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingScale]);

        var unknownPointId = Guid.CreateVersion7();
        var command = new UpdateRatingScalesCommand(
            [
                new RatingScaleInput(
                    "Custom",
                    [
                        new RatingScalePointInput("N", "Needs Improvement", 1, Id: knownPointId),
                        new RatingScalePointInput("E", "Excellent", 2, Id: unknownPointId),
                    ],
                    Id: existingScale.Id),
            ],
            ExpectedVersion: 0,
            Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRatingScalesCommandHandler.UnknownPointIdErrorCode);
        var error = result.Error.ShouldBeOfType<RatingScaleValidationError>();
        error.ScaleIndex.ShouldBe(0);
        error.PointIndex.ShouldBe(1);
        await _ratingScaleRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<RatingScale>>(), Arg.Any<CancellationToken>());
    }
}
