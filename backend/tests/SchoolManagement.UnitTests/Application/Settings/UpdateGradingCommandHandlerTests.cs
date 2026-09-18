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
/// Tests <see cref="UpdateGradingCommandHandler"/>: optimistic concurrency, the whole-scale validation
/// gate, the 6.2.9 conditional reason requirement, and that a winning save replaces every band and
/// bumps <see cref="SchoolProfile.GradingVersionNumber"/>.
/// </summary>
public sealed class UpdateGradingCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<GradingBandInput> ValidBands =
    [
        new(50, 100, "P", "Pass"),
        new(0, 49, "F", "Fail"),
    ];

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();
    private readonly ISettingsSnapshotSource _settingsSnapshotSource = Substitute.For<ISettingsSnapshotSource>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    // Defaults set in the CONSTRUCTOR, not CreateHandler(): a test's own .Returns() setup runs AFTER
    // construction but BEFORE CreateHandler() is invoked at the point of use, so only defaults set
    // here are guaranteed not to clobber it back (NSubstitute: the most recently configured .Returns()
    // for a matching call wins).
    public UpdateGradingCommandHandlerTests()
    {
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
        _settingsSnapshotSource.LoadAsync(Arg.Any<CancellationToken>()).Returns(new SettingsSnapshotState(
            Array.Empty<GradingBand>(),
            Array.Empty<AssessmentComponent>(),
            ResultRules.CreateSeed(Guid.CreateVersion7()),
            Array.Empty<RatingScale>(),
            Array.Empty<DevelopmentDomain>(),
            Array.Empty<Trait>(),
            Array.Empty<TraitBlock>()));
    }

    private UpdateGradingCommandHandler CreateHandler()
    {
        return new(
            _schoolProfileRepository,
            _gradingBandRepository,
            _configVersionRepository,
            _academicSessionRepository,
            _publishedResultsGate,
            _settingsSnapshotSource,
            _currentUser,
            _auditSink,
            _timeProvider);
    }

    private SchoolProfile CreateTrackedProfile(int gradingVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), gradingVersionNumber: gradingVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409AndWritesNoVersionRow()
    {
        CreateTrackedProfile(gradingVersionNumber: 3);

        var result = await CreateHandler().HandleAsync(
            new UpdateGradingCommand(ValidBands, ExpectedVersion: 2, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateGradingCommandHandler.StaleVersionErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
        await _gradingBandRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<GradingBand>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnInvalidScale_ReturnsTheFirstRuleFailureAndWritesNothing()
    {
        CreateTrackedProfile();

        var result = await CreateHandler().HandleAsync(
            new UpdateGradingCommand([], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GradingScaleRules.EmptyScaleCode);
        await _gradingBandRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<GradingBand>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAValidScaleAndNoActiveSession_ReplacesTheBandsAndBumpsTheVersion()
    {
        var profile = CreateTrackedProfile(gradingVersionNumber: 4);

        var result = await CreateHandler().HandleAsync(
            new UpdateGradingCommand(ValidBands, ExpectedVersion: 4, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VersionNumber.ShouldBe(5);
        result.Value.Bands.Count.ShouldBe(2);
        result.Value.Bands[0].GradeLetter.ShouldBe("P");
        profile.GradingVersionNumber.ShouldBe(5);

        await _gradingBandRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IReadOnlyList<GradingBand>>(bands => bands != null && bands.Count == 2),
            Arg.Any<CancellationToken>());
        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.ChangedGroup == ConfigVersionGroup.Grading && version.Reason == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedInTheActiveSessionAndNoReasonIsGiven_ReturnsAValidationFailure()
    {
        CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().HandleAsync(
            new UpdateGradingCommand(ValidBands, ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PublishedResultsReasonGate.ReasonRequiredErrorCode);
        await _gradingBandRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<GradingBand>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedAndAReasonIsGiven_SucceedsAndStoresTheReason()
    {
        var profile = CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().HandleAsync(
            new UpdateGradingCommand(ValidBands, ExpectedVersion: 0, Reason: "The school revised the pass mark policy."),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        profile.GradingVersionNumber.ShouldBe(1);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.Reason == "The school revised the pass mark policy."),
            Arg.Any<CancellationToken>());
    }
}
