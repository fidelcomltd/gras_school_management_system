using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="UpdateAssessmentCommandHandler"/>: optimistic concurrency, id resolution, the six
/// structural rules, and — the card's central risk — the session lock's exact boundary: rename and
/// reorder always allowed, add/remove/maximum-or-kind-change rejected once locked.
/// </summary>
public sealed class UpdateAssessmentCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IAssessmentComponentRepository _assessmentComponentRepository = Substitute.For<IAssessmentComponentRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IAcademicSessionRepository _academicSessionRepository = Substitute.For<IAcademicSessionRepository>();
    private readonly IPublishedResultsGate _publishedResultsGate = Substitute.For<IPublishedResultsGate>();
    private readonly ISubjectScoreSessionLockLookup _subjectScoreSessionLockLookup = Substitute.For<ISubjectScoreSessionLockLookup>();
    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IDevelopmentDomainRepository _developmentDomainRepository = Substitute.For<IDevelopmentDomainRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    private readonly AssessmentComponent _firstCa =
        AssessmentComponent.Create(Guid.CreateVersion7(), "1st CA", "CA1", 20, isExamination: false, displayOrder: 1);

    private readonly AssessmentComponent _secondCa =
        AssessmentComponent.Create(Guid.CreateVersion7(), "2nd CA", "CA2", 20, isExamination: false, displayOrder: 2);

    private readonly AssessmentComponent _exam =
        AssessmentComponent.Create(Guid.CreateVersion7(), "Exam", "EXAM", 60, isExamination: true, displayOrder: 3);

    // Defaults set in the CONSTRUCTOR, not CreateHandler() — see UpdateGradingCommandHandlerTests'
    // own constructor comment for why: a test's own .Returns() (for example StubActiveSession) runs
    // AFTER construction but BEFORE CreateHandler() is invoked, so only a constructor default is
    // guaranteed not to clobber it back to null/empty.
    public UpdateAssessmentCommandHandlerTests()
    {
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingBand>());
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns((AcademicSession?)null);
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(ResultRules.CreateSeed(Guid.CreateVersion7()));
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<DevelopmentDomain>());
    }

    private UpdateAssessmentCommandHandler CreateHandler()
    {
        return new(
            _schoolProfileRepository,
            _assessmentComponentRepository,
            _gradingBandRepository,
            _configVersionRepository,
            _academicSessionRepository,
            _publishedResultsGate,
            _subjectScoreSessionLockLookup,
            _resultRulesRepository,
            _ratingScaleRepository,
            _developmentDomainRepository,
            _currentUser,
            _auditSink,
            _timeProvider);
    }

    private SchoolProfile CreateTrackedProfile(int assessmentVersionNumber = 0)
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), assessmentVersionNumber: assessmentVersionNumber);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        return profile;
    }

    private void SeedExistingComponents()
    {
        _assessmentComponentRepository.ListTrackedAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AssessmentComponent>)[_firstCa, _secondCa, _exam]);
    }

    private AcademicSession StubActiveSession(bool locked)
    {
        var session = AcademicSession.Create(Guid.CreateVersion7(), "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        _academicSessionRepository.FindActiveAsync(Arg.Any<CancellationToken>()).Returns(session);
        _subjectScoreSessionLockLookup.AnyScoreExistsInSessionAsync(session.Id, Arg.Any<CancellationToken>()).Returns(locked);
        return session;
    }

    private static string IdOf(AssessmentComponent component) => component.Id.ToString("D", CultureInfo.InvariantCulture);

    [Fact]
    public async Task HandleAsync_WithAStaleExpectedVersion_Returns409()
    {
        CreateTrackedProfile(assessmentVersionNumber: 2);

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand([], ExpectedVersion: 1, Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.StaleVersionErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithAnIdThatMatchesNoExistingComponent_ReturnsAValidationFailure()
    {
        CreateTrackedProfile();
        SeedExistingComponents();

        var unknownId = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);
        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [new AssessmentComponentSaveRequest(unknownId, "CA", "CA", 40, false)],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.UnknownComponentIdErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithTheSameIdSubmittedTwice_ReturnsAValidationFailure()
    {
        CreateTrackedProfile();
        SeedExistingComponents();

        var id = IdOf(_firstCa);
        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(id, "1st CA", "CA1", 20, false),
                    new AssessmentComponentSaveRequest(id, "1st CA dup", "CA1B", 20, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.DuplicateComponentIdErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithAStructurallyInvalidSubmission_ReturnsTheStructuralRuleFailure()
    {
        CreateTrackedProfile();
        SeedExistingComponents();

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(null, "CA", "CA", 30, false),
                    new AssessmentComponentSaveRequest(null, "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(AssessmentStructureRules.DoesNotTotalHundredCode);
    }

    [Fact]
    public async Task HandleAsync_WithNoActiveSession_AllowsAddingAndRemovingComponentsFreely()
    {
        var profile = CreateTrackedProfile(assessmentVersionNumber: 7);
        SeedExistingComponents();

        // Drop 2nd CA, add Assignment, keep 1st CA and Exam — a genuine add AND a genuine remove.
        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "1st CA", "CA1", 20, false),
                    new AssessmentComponentSaveRequest(null, "Assignment", "ASSGN", 20, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 7,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Components.Count.ShouldBe(3);
        profile.AssessmentVersionNumber.ShouldBe(8);

        await _assessmentComponentRepository.Received(1).AddAsync(
            Arg.Is<AssessmentComponent>(c => c != null && c.Name == "Assignment"),
            Arg.Any<CancellationToken>());
        await _assessmentComponentRepository.Received(1).RemoveAsync(_secondCa, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AlwaysPlacesTheExaminationComponentLastRegardlessOfSubmittedPosition()
    {
        CreateTrackedProfile();
        SeedExistingComponents();

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "1st CA", "CA1", 20, false),
                    new AssessmentComponentSaveRequest(IdOf(_secondCa), "2nd CA", "CA2", 20, false),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Components[^1].IsExamination.ShouldBeTrue();
        result.Value.Components[^1].DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_WhenLockedAndOnlyRenamingAndReordering_Succeeds()
    {
        CreateTrackedProfile();
        SeedExistingComponents();
        StubActiveSession(locked: true);

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_secondCa), "Second CA Test", "CA2", 20, false),
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "First CA Test", "CA1", 20, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _firstCa.Name.ShouldBe("First CA Test");
        _secondCa.Name.ShouldBe("Second CA Test");
    }

    [Fact]
    public async Task HandleAsync_WhenLockedAndAddingAComponent_Returns409WithTheSessionLockMessage()
    {
        CreateTrackedProfile();
        SeedExistingComponents();
        var session = StubActiveSession(locked: true);

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "1st CA", "CA1", 15, false),
                    new AssessmentComponentSaveRequest(IdOf(_secondCa), "2nd CA", "CA2", 15, false),
                    new AssessmentComponentSaveRequest(null, "Assignment", "ASSGN", 10, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.StructureLockedErrorCode);
        result.Error.Description.ShouldContain(session.Name);
        await _assessmentComponentRepository.DidNotReceive().AddAsync(Arg.Any<AssessmentComponent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenLockedAndRemovingAComponent_Returns409()
    {
        CreateTrackedProfile();
        SeedExistingComponents();
        StubActiveSession(locked: true);

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "1st CA", "CA1", 40, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.StructureLockedErrorCode);
        await _assessmentComponentRepository.DidNotReceive().RemoveAsync(Arg.Any<AssessmentComponent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenLockedAndChangingAnExistingMaximum_Returns409()
    {
        CreateTrackedProfile();
        SeedExistingComponents();
        StubActiveSession(locked: true);

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "1st CA", "CA1", 25, false),
                    new AssessmentComponentSaveRequest(IdOf(_secondCa), "2nd CA", "CA2", 15, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 60, true),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.StructureLockedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenLockedAndFlippingWhichComponentIsTheExamination_Returns409()
    {
        CreateTrackedProfile();
        SeedExistingComponents();
        StubActiveSession(locked: true);

        var result = await CreateHandler().HandleAsync(
            new UpdateAssessmentCommand(
                [
                    new AssessmentComponentSaveRequest(IdOf(_firstCa), "1st CA", "CA1", 60, true),
                    new AssessmentComponentSaveRequest(IdOf(_secondCa), "2nd CA", "CA2", 20, false),
                    new AssessmentComponentSaveRequest(IdOf(_exam), "Exam", "EXAM", 20, false),
                ],
                ExpectedVersion: 0,
                Reason: null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAssessmentCommandHandler.StructureLockedErrorCode);
    }
}
