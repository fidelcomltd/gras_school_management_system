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
/// Tests <see cref="UpdateTraitsCommandHandler"/>: optimistic concurrency, unknown scale/trait ids,
/// id-stable resave/rename, the archive-never-gated vs removal-gated distinction (spec 6.2.7's exact
/// refusal message), and that traits are NOT section-scoped.
/// </summary>
public sealed class UpdateTraitsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AffectiveScaleId = Guid.CreateVersion7();
    private static readonly Guid PsychomotorScaleId = Guid.CreateVersion7();

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly ITraitRepository _traitRepository = Substitute.For<ITraitRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();
    private readonly ITraitUsageGate _traitUsageGate = Substitute.For<ITraitUsageGate>();
    private readonly ISettingsSnapshotSource _settingsSnapshotSource = Substitute.For<ISettingsSnapshotSource>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    public UpdateTraitsCommandHandlerTests()
    {
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(
        [
            RatingScale.Create(AffectiveScaleId, "Primary trait", [RatingScalePoint.Create(Guid.CreateVersion7(), AffectiveScaleId, "E", "Excellent", 1)]),
            RatingScale.Create(PsychomotorScaleId, "Also primary trait", [RatingScalePoint.Create(Guid.CreateVersion7(), PsychomotorScaleId, "E", "Excellent", 1)]),
        ]);
        _traitRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Trait>());
        _traitUsageGate.HasEverBeenRatedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _settingsSnapshotSource.LoadAsync(Arg.Any<CancellationToken>()).Returns(new SettingsSnapshotState(
            Array.Empty<GradingBand>(),
            Array.Empty<AssessmentComponent>(),
            ResultRules.CreateSeed(Guid.CreateVersion7()),
            Array.Empty<RatingScale>(),
            Array.Empty<DevelopmentDomain>(),
            Array.Empty<Trait>(),
            Array.Empty<TraitBlock>()));
    }

    private UpdateTraitsCommandHandler CreateHandler() => new(
        _schoolProfileRepository,
        _traitRepository,
        _ratingScaleRepository,
        _configVersionRepository,
        _academicSessionRepository,
        _publishedResultsGate,
        _traitUsageGate,
        _settingsSnapshotSource,
        _currentUser,
        _auditSink,
        _timeProvider);

    private SchoolProfile CreateTrackedProfile(int traitsVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), traitsVersionNumber: traitsVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    private static UpdateTraitsCommand ValidCommand(IReadOnlyList<TraitInput> traits, int expectedVersion = 0, string? reason = null) =>
        new(AffectiveScaleId, PsychomotorScaleId, traits, expectedVersion, reason);

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409AndWritesNoVersionRow()
    {
        CreateTrackedProfile(traitsVersionNumber: 3);

        var result = await CreateHandler().HandleAsync(
            ValidCommand([], expectedVersion: 2),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateTraitsCommandHandler.StaleVersionErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
        await _traitRepository.DidNotReceive().ReplaceAllAsync(
            Arg.Any<IReadOnlyList<Trait>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownAffectiveRatingScaleId_Returns422()
    {
        CreateTrackedProfile();

        var command = new UpdateTraitsCommand(Guid.CreateVersion7(), PsychomotorScaleId, [], ExpectedVersion: 0, Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateTraitsCommandHandler.UnknownScaleIdErrorCode);
        await _traitRepository.DidNotReceive().ReplaceAllAsync(
            Arg.Any<IReadOnlyList<Trait>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownPsychomotorRatingScaleId_Returns422()
    {
        CreateTrackedProfile();

        var command = new UpdateTraitsCommand(AffectiveScaleId, Guid.CreateVersion7(), [], ExpectedVersion: 0, Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateTraitsCommandHandler.UnknownScaleIdErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithADuplicateNameWithinTheSameDomain_Returns422()
    {
        CreateTrackedProfile();

        var command = ValidCommand(
        [
            new TraitInput(TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active),
            new TraitInput(TraitDomain.Affective, "punctuality", 2, TraitStatus.Active),
        ]);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TraitRules.DuplicateNameCode);
        await _traitRepository.DidNotReceive().ReplaceAllAsync(
            Arg.Any<IReadOnlyList<Trait>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownTraitId_Returns422NamingTheTraitIndex()
    {
        CreateTrackedProfile();

        var unknownId = Guid.CreateVersion7();
        var command = ValidCommand([new TraitInput(TraitDomain.Affective, "Ghost", 1, TraitStatus.Active, unknownId)]);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateTraitsCommandHandler.UnknownTraitIdErrorCode);
        var error = result.Error.ShouldBeOfType<TraitValidationError>();
        error.TraitIndex.ShouldBe(0);
        await _traitRepository.DidNotReceive().ReplaceAllAsync(
            Arg.Any<IReadOnlyList<Trait>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ResavingWithoutChanges_KeepsEveryTraitId()
    {
        CreateTrackedProfile();

        var traitId = Guid.CreateVersion7();
        var existingTrait = Trait.Create(traitId, TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active);
        _traitRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingTrait]);

        var resubmitted = new TraitInput(TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active, traitId);

        var result = await CreateHandler().HandleAsync(ValidCommand([resubmitted]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Traits[0].Id.ShouldBe(traitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        // Every id was present in the submission, so nothing was removed — the gate is never asked.
        await _traitUsageGate.DidNotReceive().HasEverBeenRatedAsync(traitId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RenamingAnExistingTraitById_KeepsItsIdAndNeverConsultsTheUsageGate()
    {
        CreateTrackedProfile();

        var traitId = Guid.CreateVersion7();
        var existingTrait = Trait.Create(traitId, TraitDomain.Affective, "Old name", 1, TraitStatus.Active);
        _traitRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingTrait]);

        var renamed = new TraitInput(TraitDomain.Affective, "New name", 1, TraitStatus.Active, traitId);

        var result = await CreateHandler().HandleAsync(ValidCommand([renamed]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Traits[0].Id.ShouldBe(traitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Traits[0].Name.ShouldBe("New name");
        await _traitUsageGate.DidNotReceive().HasEverBeenRatedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ArchivingATraitBySubmittingItsIdWithArchivedStatus_IsAlwaysAllowedAndNeverGated()
    {
        // Spec 6.2.7: archiving is the safe path and is never gated, even when ratings exist — the id
        // stays present in the submission, only its status changes, so this is never a removal.
        CreateTrackedProfile();

        var traitId = Guid.CreateVersion7();
        var existingTrait = Trait.Create(traitId, TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active);
        _traitRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingTrait]);
        _traitUsageGate.HasEverBeenRatedAsync(traitId, Arg.Any<CancellationToken>()).Returns(true);

        var archived = new TraitInput(TraitDomain.Affective, "Punctuality", 1, TraitStatus.Archived, traitId);

        var result = await CreateHandler().HandleAsync(ValidCommand([archived]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Traits[0].Status.ShouldBe(TraitStatus.Archived);
        await _traitUsageGate.DidNotReceive().HasEverBeenRatedAsync(traitId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RemovingARatedTrait_Returns409WithTheExactMessage()
    {
        CreateTrackedProfile();

        var traitId = Guid.CreateVersion7();
        var existingTrait = Trait.Create(traitId, TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active);
        _traitRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingTrait]);
        _traitUsageGate.HasEverBeenRatedAsync(traitId, Arg.Any<CancellationToken>()).Returns(true);

        // The trait's id is omitted from the submission — a removal.
        var result = await CreateHandler().HandleAsync(ValidCommand([]), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateTraitsCommandHandler.TraitRatedErrorCode);
        result.Error.Description.ShouldBe(
            "Ratings have already been entered for Punctuality this term. Archive the trait instead, " +
            "which keeps it on this term's sheets and removes it from next term.");
        await _traitRepository.DidNotReceive().ReplaceAllAsync(
            Arg.Any<IReadOnlyList<Trait>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RemovingAnUnratedTrait_Succeeds()
    {
        CreateTrackedProfile();

        var traitId = Guid.CreateVersion7();
        var existingTrait = Trait.Create(traitId, TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active);
        _traitRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingTrait]);

        var result = await CreateHandler().HandleAsync(ValidCommand([]), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _traitRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IReadOnlyList<Trait>>(traits => traits != null && traits.Count == 0),
            AffectiveScaleId,
            PsychomotorScaleId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAValidSetAndNoActiveSession_ReplacesTraitsAndBumpsTheVersion()
    {
        var profile = CreateTrackedProfile(traitsVersionNumber: 4);

        var command = ValidCommand(
            [
                new TraitInput(TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active),
                new TraitInput(TraitDomain.Psychomotor, "Sports", 1, TraitStatus.Active),
            ],
            expectedVersion: 4);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VersionNumber.ShouldBe(5);
        result.Value.Traits.Count.ShouldBe(2);
        result.Value.AffectiveRatingScaleId.ShouldBe(AffectiveScaleId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.PsychomotorRatingScaleId.ShouldBe(PsychomotorScaleId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        profile.TraitsVersionNumber.ShouldBe(5);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.ChangedGroup == ConfigVersionGroup.Traits && version.Reason == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedInTheActiveSessionAndNoReasonIsGiven_ReturnsAValidationFailure()
    {
        CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().HandleAsync(ValidCommand([]), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PublishedResultsReasonGate.ReasonRequiredErrorCode);
        await _traitRepository.DidNotReceive().ReplaceAllAsync(
            Arg.Any<IReadOnlyList<Trait>>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedAndAReasonIsGiven_SucceedsAndStoresTheReason()
    {
        var profile = CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().HandleAsync(
            ValidCommand([], reason: "The school renamed a trait."),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        profile.TraitsVersionNumber.ShouldBe(1);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.Reason == "The school renamed a trait."),
            Arg.Any<CancellationToken>());
    }
}
