using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="SaveTraitRatingsHandler"/>: every error code, Q1-A's omitted-vs-null cell
/// semantics, ruling R1's section gate, and that an existing result set is never flagged
/// <c>needsRecompute</c> by a rating-only save (spec §6.7.12 amendment).
/// </summary>
public sealed class SaveTraitRatingsHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid SectionId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid TraitId = Guid.CreateVersion7();
    private static readonly Guid OtherTraitId = Guid.CreateVersion7();
    private static readonly Guid ArchivedTraitId = Guid.CreateVersion7();
    private static readonly Guid AffectiveScaleId = Guid.CreateVersion7();
    private static readonly Guid PsychomotorScaleId = Guid.CreateVersion7();
    private static readonly Guid AffectivePointId = Guid.CreateVersion7();
    private static readonly Guid WrongScalePointId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IClassLevelRepository _classLevels = Substitute.For<IClassLevelRepository>();
    private readonly ISectionRepository _sections = Substitute.For<ISectionRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly ITraitRepository _traits = Substitute.For<ITraitRepository>();
    private readonly IRatingScaleRepository _ratingScales = Substitute.For<IRatingScaleRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly ITraitRatingRepository _traitRatings = Substitute.For<ITraitRatingRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public SaveTraitRatingsHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        var level = ClassLevel.Create(ClassLevelId, "Primary 1", SectionId, 1, null).Value;
        _classLevels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([level]);

        var section = Section.Create(SectionId, "Primary", ratesTraits: true).Value;
        _sections.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([section]);

        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((SchoolManagement.Domain.Sessions.AcademicSession?)null);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        _traits.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Trait.Create(TraitId, TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active),
            Trait.Create(OtherTraitId, TraitDomain.Affective, "Conduct", 2, TraitStatus.Active),
            Trait.Create(ArchivedTraitId, TraitDomain.Affective, "Retired", 3, TraitStatus.Archived),
        ]);

        _traits.ListBlocksReadOnlyAsync(Arg.Any<CancellationToken>()).Returns(
        [
            TraitBlock.Create(TraitDomain.Affective, AffectiveScaleId),
            TraitBlock.Create(TraitDomain.Psychomotor, PsychomotorScaleId),
        ]);

        var affectiveScale = RatingScale.Create(
            AffectiveScaleId, "Primary trait", [RatingScalePoint.Create(AffectivePointId, AffectiveScaleId, "E", "Excellent", 1)]);
        var psychomotorScale = RatingScale.Create(
            PsychomotorScaleId, "Other scale", [RatingScalePoint.Create(WrongScalePointId, PsychomotorScaleId, "S", "Sports", 1)]);
        _ratingScales.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([affectiveScale, psychomotorScale]);

        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
        _traitRatings.ListTrackedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private SaveTraitRatingsHandler CreateHandler() => new(
        _arms, _sessions, _terms, _classLevels, _sections, _enrolments, _traits, _ratingScales, _resultSets, _traitRatings, _currentUser, _auditSink);

    private static SaveTraitRatingsCommand Command(string? version, params SaveTraitRatingsRowInput[] rows) =>
        new(ArmId.ToString(), TermId.ToString(), version, rows);

    [Fact]
    public async Task HandleAsync_WhenTheArmsSectionDoesNotRateTraits_Returns422()
    {
        var nurserySection = Section.Create(SectionId, "Nursery", ratesTraits: false).Value;
        _sections.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([nurserySection]);

        var result = await CreateHandler().HandleAsync(Command(null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GetTraitRatingsHandler.SectionNotRatedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenTheSessionIsClosed_Returns409()
    {
        var closedSession = SchoolManagement.Domain.Sessions.AcademicSession.Create(
            SessionId, "2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 31)).Value;
        closedSession.Activate();
        closedSession.Close();
        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns(closedSession);

        var result = await CreateHandler().HandleAsync(Command(null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("trait_ratings.session_closed");
    }

    [Fact]
    public async Task HandleAsync_APupilNotOnTheRoster_Returns422()
    {
        var row = new SaveTraitRatingsRowInput(Guid.CreateVersion7().ToString(), new Dictionary<string, string?>());

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].PupilId");
    }

    [Fact]
    public async Task HandleAsync_AnUnknownTraitId_Returns422()
    {
        var unknownTraitId = Guid.CreateVersion7();
        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [unknownTraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("This is not a known trait.");
    }

    [Fact]
    public async Task HandleAsync_AnArchivedTraitId_Returns422()
    {
        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [ArchivedTraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("This trait is archived and cannot be rated.");
    }

    [Fact]
    public async Task HandleAsync_APointFromTheWrongScale_Returns422()
    {
        // TraitId is affective, so it is rated on the affective scale — WrongScalePointId belongs to
        // the psychomotor scale.
        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = WrongScalePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("This point is not on Primary trait.");
    }

    [Fact]
    public async Task HandleAsync_FirstRatingForTheArm_CreatesTheResultSetInDraft()
    {
        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResultSet.ShouldNotBeNull();
        result.Value.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        result.Value.ResultSet!.NeedsRecompute.ShouldBeTrue();
        await _resultSets.Received(1).AddAsync(Arg.Any<ResultSet>(), Arg.Any<CancellationToken>());
        await _traitRatings.Received(1).AddAsync(Arg.Any<TraitRating>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OmittedTraitKey_LeavesTheExistingRatingUntouched()
    {
        // Q1-A ruling: a key absent from Ratings leaves that trait's existing rating alone.
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);

        var existingRating = TraitRating.Create(Guid.CreateVersion7(), resultSet.Id, PupilId, OtherTraitId, AffectivePointId).Value;
        _traitRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingRating]);

        var currentVersion = TraitRatingVersion.Compute([new TraitRatingSnapshot(PupilId, OtherTraitId, AffectivePointId)]);

        // Save touches ONLY TraitId, never mentioning OtherTraitId.
        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var otherTraitRating = result.Value.Rows.Single().Ratings[OtherTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)];
        otherTraitRating.ShouldBe(AffectivePointId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        await _traitRatings.DidNotReceive().RemoveAsync(Arg.Any<TraitRating>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExplicitNullClearsTheExistingRating()
    {
        // Q1-A ruling: a key present with an explicit null deletes that trait's rating.
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);

        var existingRating = TraitRating.Create(Guid.CreateVersion7(), resultSet.Id, PupilId, TraitId, AffectivePointId).Value;
        _traitRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingRating]);

        var currentVersion = TraitRatingVersion.Compute([new TraitRatingSnapshot(PupilId, TraitId, AffectivePointId)]);

        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?> { [TraitId.ToString()] = null });

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Rows.Single().Ratings[TraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)].ShouldBeNull();
        await _traitRatings.Received(1).RemoveAsync(existingRating, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _traitRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);

        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command("a-stale-hash", row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveTraitRatingsHandler.StaleVersionErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResultSetIsApproved_Returns409Locked()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _traitRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);

        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveTraitRatingsHandler.ResultSetLockedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_OnAnExistingResultSet_NeverFlagsNeedsRecompute()
    {
        // Spec §6.7.12 amendment: ratings are never converted to marks and never computed, so unlike
        // a score save this must NOT re-trigger needs_recompute on an existing set.
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        resultSet.MarkComputed(null, DateTimeOffset.UtcNow, pupilCount: 1);
        resultSet.NeedsRecompute.ShouldBeFalse();
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _traitRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);

        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        resultSet.NeedsRecompute.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_OnASuccessfulSave_RecordsAnAuditEvent()
    {
        var row = new SaveTraitRatingsRowInput(PupilId.ToString(), new Dictionary<string, string?>
        {
            [TraitId.ToString()] = AffectivePointId.ToString(),
        });

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _auditSink.Received(1).RecordAsync(
            Arg.Is(Privileges.Results.TraitEnter), Arg.Is("trait_rating"), Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>>());
    }
}
