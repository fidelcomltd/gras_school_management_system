using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="UpdateDevelopmentDomainsCommandHandler"/>: optimistic concurrency, unknown
/// section/scale/domain/indicator ids, id-stable resave/rename, the archive-never-gated vs
/// removal-gated distinction (spec 6.2.13's exact refusal message), an omitted domain cascading
/// through all of its own indicators, and <c>activeIndicatorCount</c>.
/// </summary>
public sealed class UpdateDevelopmentDomainsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SectionId = Guid.CreateVersion7();
    private static readonly Guid RatingScaleId = Guid.CreateVersion7();

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IDevelopmentDomainRepository _developmentDomainRepository = Substitute.For<IDevelopmentDomainRepository>();
    private readonly ISectionRepository _sectionRepository = Substitute.For<ISectionRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();

    private readonly IDevelopmentIndicatorUsageGate _developmentIndicatorUsageGate =
        Substitute.For<IDevelopmentIndicatorUsageGate>();

    private readonly ISettingsSnapshotSource _settingsSnapshotSource = Substitute.For<ISettingsSnapshotSource>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    public UpdateDevelopmentDomainsCommandHandlerTests()
    {
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(
            [RatingScale.Create(RatingScaleId, "Nursery development", [RatingScalePoint.Create(Guid.CreateVersion7(), RatingScaleId, "E", "Excellent", 1)])]);
        _sectionRepository.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([Section.Create(SectionId, "Nursery").Value]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<DevelopmentDomain>());
        _developmentIndicatorUsageGate.HasEverBeenRatedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _settingsSnapshotSource.LoadAsync(Arg.Any<CancellationToken>()).Returns(new SettingsSnapshotState(
            Array.Empty<GradingBand>(),
            Array.Empty<AssessmentComponent>(),
            ResultRules.CreateSeed(Guid.CreateVersion7()),
            Array.Empty<RatingScale>(),
            Array.Empty<DevelopmentDomain>()));
    }

    private UpdateDevelopmentDomainsCommandHandler CreateHandler() => new(
        _schoolProfileRepository,
        _developmentDomainRepository,
        _sectionRepository,
        _ratingScaleRepository,
        _configVersionRepository,
        _academicSessionRepository,
        _publishedResultsGate,
        _developmentIndicatorUsageGate,
        _settingsSnapshotSource,
        _currentUser,
        _auditSink,
        _timeProvider);

    private SchoolProfile CreateTrackedProfile(int developmentDomainsVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), developmentDomainsVersionNumber: developmentDomainsVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    private static DevelopmentDomainInput ValidDomain(Guid? id = null, IReadOnlyList<DevelopmentIndicatorInput>? indicators = null) =>
        new(
            SectionId,
            "Personal & Physical Development",
            1,
            RatingScaleId,
            true,
            DevelopmentDomainStatus.Active,
            indicators ?? [new DevelopmentIndicatorInput("Potty trained", 1, DevelopmentIndicatorStatus.Active)],
            id);

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409AndWritesNoVersionRow()
    {
        CreateTrackedProfile(developmentDomainsVersionNumber: 3);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([], ExpectedVersion: 2, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.StaleVersionErrorCode);
        await _configVersionRepository.DidNotReceive().AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
        await _developmentDomainRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<DevelopmentDomain>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownSectionId_Returns422NamingTheDomainIndex()
    {
        CreateTrackedProfile();

        var command = new UpdateDevelopmentDomainsCommand(
            [new DevelopmentDomainInput(Guid.CreateVersion7(), "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active, [])],
            ExpectedVersion: 0,
            Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.UnknownSectionIdErrorCode);
        var error = result.Error.ShouldBeOfType<DevelopmentDomainValidationError>();
        error.DomainIndex.ShouldBe(0);
        await _developmentDomainRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<DevelopmentDomain>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownRatingScaleId_Returns422NamingTheDomainIndex()
    {
        CreateTrackedProfile();

        var command = new UpdateDevelopmentDomainsCommand(
            [new DevelopmentDomainInput(SectionId, "Custom", 1, Guid.CreateVersion7(), true, DevelopmentDomainStatus.Active, [])],
            ExpectedVersion: 0,
            Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.UnknownRatingScaleIdErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownDomainId_Returns422NamingTheDomainIndex()
    {
        CreateTrackedProfile();

        var command = new UpdateDevelopmentDomainsCommand([ValidDomain(id: Guid.CreateVersion7())], ExpectedVersion: 0, Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.UnknownDomainIdErrorCode);
        var error = result.Error.ShouldBeOfType<DevelopmentDomainValidationError>();
        error.DomainIndex.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownIndicatorIdOnAnExistingDomain_Returns422NamingDomainAndIndicatorIndex()
    {
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var knownIndicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(knownIndicatorId, domainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active)]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);

        var unknownIndicatorId = Guid.CreateVersion7();
        var command = new UpdateDevelopmentDomainsCommand(
            [
                new DevelopmentDomainInput(
                    SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
                    [new DevelopmentIndicatorInput("Ghost", 1, DevelopmentIndicatorStatus.Active, unknownIndicatorId)],
                    domainId),
            ],
            ExpectedVersion: 0,
            Reason: null);

        var result = await CreateHandler().HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.UnknownIndicatorIdErrorCode);
        var error = result.Error.ShouldBeOfType<DevelopmentDomainValidationError>();
        error.DomainIndex.ShouldBe(0);
        error.IndicatorIndex.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_ResavingWithoutChanges_KeepsEveryDomainAndIndicatorId()
    {
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var indicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(indicatorId, domainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active)]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);

        var resubmitted = new DevelopmentDomainInput(
            SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [new DevelopmentIndicatorInput("Potty trained", 1, DevelopmentIndicatorStatus.Active, indicatorId)],
            domainId);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([resubmitted], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Domains[0].Id.ShouldBe(domainId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Domains[0].Indicators[0].Id.ShouldBe(indicatorId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        // Every id was present in the submission, so nothing was removed — the gate is never asked.
        await _developmentIndicatorUsageGate.DidNotReceive().HasEverBeenRatedAsync(indicatorId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RenamingAnExistingDomainById_KeepsItsIdAndNeverConsultsTheUsageGate()
    {
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var indicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Old name", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(indicatorId, domainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active)]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);

        var renamed = new DevelopmentDomainInput(
            SectionId, "New name", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [new DevelopmentIndicatorInput("Potty trained", 1, DevelopmentIndicatorStatus.Active, indicatorId)],
            domainId);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([renamed], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Domains[0].Id.ShouldBe(domainId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Domains[0].Name.ShouldBe("New name");
        await _developmentIndicatorUsageGate.DidNotReceive().HasEverBeenRatedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ArchivingAnIndicatorBySubmittingItsIdWithArchivedStatus_IsAlwaysAllowedAndNeverGated()
    {
        // Spec 6.2.13: "archiving a submitted id is always allowed and is never gated" — the id stays
        // present in the submission, only its status changes, so this is never a removal.
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var indicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(indicatorId, domainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active)]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);
        _developmentIndicatorUsageGate.HasEverBeenRatedAsync(indicatorId, Arg.Any<CancellationToken>()).Returns(true);

        var archived = new DevelopmentDomainInput(
            SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [new DevelopmentIndicatorInput("Potty trained", 1, DevelopmentIndicatorStatus.Archived, indicatorId)],
            domainId);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([archived], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Domains[0].Indicators[0].Status.ShouldBe(DevelopmentIndicatorStatus.Archived);
        await _developmentIndicatorUsageGate.DidNotReceive().HasEverBeenRatedAsync(indicatorId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RemovingARatedIndicatorFromARetainedDomain_Returns409WithTheExactMessage()
    {
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var indicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(indicatorId, domainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active)]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);
        _developmentIndicatorUsageGate.HasEverBeenRatedAsync(indicatorId, Arg.Any<CancellationToken>()).Returns(true);

        // The indicator's id is omitted from the domain's resubmitted indicator list — a removal.
        var domainMissingTheIndicator = new DevelopmentDomainInput(
            SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active, [], domainId);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([domainMissingTheIndicator], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.IndicatorRatedErrorCode);
        result.Error.Description.ShouldBe(
            "Ratings have already been entered for Potty trained this term. Archive the indicator " +
            "instead, which keeps it on this term's sheets and removes it from next term.");
        await _developmentDomainRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<DevelopmentDomain>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OmittingAWholeDomainWithARatedIndicator_Returns409TrippedByTheFirstRatedIndicator()
    {
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var unratedIndicatorId = Guid.CreateVersion7();
        var ratedIndicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [
                DevelopmentIndicator.Create(unratedIndicatorId, domainId, "Never rated", 1, DevelopmentIndicatorStatus.Active),
                DevelopmentIndicator.Create(ratedIndicatorId, domainId, "Potty trained", 2, DevelopmentIndicatorStatus.Active),
            ]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);
        _developmentIndicatorUsageGate.HasEverBeenRatedAsync(unratedIndicatorId, Arg.Any<CancellationToken>()).Returns(false);
        _developmentIndicatorUsageGate.HasEverBeenRatedAsync(ratedIndicatorId, Arg.Any<CancellationToken>()).Returns(true);

        // The whole domain is omitted from the submission.
        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateDevelopmentDomainsCommandHandler.IndicatorRatedErrorCode);
        await _developmentIndicatorUsageGate.Received(1).HasEverBeenRatedAsync(unratedIndicatorId, Arg.Any<CancellationToken>());
        await _developmentDomainRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<DevelopmentDomain>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OmittingAWholeDomainWithNoRatedIndicator_Succeeds()
    {
        CreateTrackedProfile();

        var domainId = Guid.CreateVersion7();
        var indicatorId = Guid.CreateVersion7();
        var existingDomain = DevelopmentDomain.Create(
            domainId, SectionId, "Custom", 1, RatingScaleId, true, DevelopmentDomainStatus.Active,
            [DevelopmentIndicator.Create(indicatorId, domainId, "Never rated", 1, DevelopmentIndicatorStatus.Active)]);
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([existingDomain]);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _developmentDomainRepository.Received(1).ReplaceAllAsync(
            Arg.Is<IReadOnlyList<DevelopmentDomain>>(domains => domains != null && domains.Count == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAValidDomain_BumpsTheVersionAndComputesActiveIndicatorCount()
    {
        var profile = CreateTrackedProfile(developmentDomainsVersionNumber: 4);

        var domain = ValidDomain(indicators:
        [
            new DevelopmentIndicatorInput("Active one", 1, DevelopmentIndicatorStatus.Active),
            new DevelopmentIndicatorInput("Archived one", 2, DevelopmentIndicatorStatus.Archived),
        ]);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([domain], ExpectedVersion: 4, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VersionNumber.ShouldBe(5);
        profile.DevelopmentDomainsVersionNumber.ShouldBe(5);
        result.Value.Domains[0].ActiveIndicatorCount.ShouldBe(1);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => version != null && version.ChangedGroup == ConfigVersionGroup.DevelopmentDomains),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AnArchivedDomain_ReportsZeroActiveIndicatorCountRegardlessOfIndicatorStatus()
    {
        CreateTrackedProfile();

        var domain = new DevelopmentDomainInput(
            SectionId, "Archived domain", 1, RatingScaleId, true, DevelopmentDomainStatus.Archived,
            [new DevelopmentIndicatorInput("Still marked active", 1, DevelopmentIndicatorStatus.Active)]);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([domain], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Domains[0].ActiveIndicatorCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_WhenAResultSetIsPublishedInTheActiveSessionAndNoReasonIsGiven_ReturnsAValidationFailure()
    {
        CreateTrackedProfile();

        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _publishedResultsGate.CountPublishedInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await CreateHandler().HandleAsync(
            new UpdateDevelopmentDomainsCommand([ValidDomain()], ExpectedVersion: 0, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PublishedResultsReasonGate.ReasonRequiredErrorCode);
        await _developmentDomainRepository.DidNotReceive().ReplaceAllAsync(Arg.Any<IReadOnlyList<DevelopmentDomain>>(), Arg.Any<CancellationToken>());
    }
}
