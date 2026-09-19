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
/// Tests <see cref="SaveDevelopmentRatingsHandler"/>: every error code, Q1-A's omitted-vs-null cell
/// semantics, Q3-A's comment rules, ruling R1's section gate, and that an existing result set is never
/// flagged <c>needsRecompute</c> by a rating-only save (spec §6.7.12 amendment).
/// </summary>
public sealed class SaveDevelopmentRatingsHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid SectionId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid DomainId = Guid.CreateVersion7();
    private static readonly Guid NoCommentDomainId = Guid.CreateVersion7();
    private static readonly Guid IndicatorId = Guid.CreateVersion7();
    private static readonly Guid OtherIndicatorId = Guid.CreateVersion7();
    private static readonly Guid ArchivedIndicatorId = Guid.CreateVersion7();
    private static readonly Guid NoCommentIndicatorId = Guid.CreateVersion7();
    private static readonly Guid ScaleId = Guid.CreateVersion7();
    private static readonly Guid OtherScaleId = Guid.CreateVersion7();
    private static readonly Guid PointId = Guid.CreateVersion7();
    private static readonly Guid WrongScalePointId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly IAcademicSessionRepository _sessions = Substitute.For<IAcademicSessionRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IClassLevelRepository _classLevels = Substitute.For<IClassLevelRepository>();
    private readonly ISectionRepository _sections = Substitute.For<ISectionRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IDevelopmentDomainRepository _developmentDomains = Substitute.For<IDevelopmentDomainRepository>();
    private readonly IRatingScaleRepository _ratingScales = Substitute.For<IRatingScaleRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly IDevelopmentRatingRepository _developmentRatings = Substitute.For<IDevelopmentRatingRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public SaveDevelopmentRatingsHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        var level = ClassLevel.Create(ClassLevelId, "Nursery 1", SectionId, 1, null).Value;
        _classLevels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([level]);

        var section = Section.Create(SectionId, "Nursery").Value;
        _sections.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([section]);

        _sessions.FindReadOnlyByIdAsync(SessionId, Arg.Any<CancellationToken>()).Returns((SchoolManagement.Domain.Sessions.AcademicSession?)null);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        var indicator = DevelopmentIndicator.Create(IndicatorId, DomainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active);
        var otherIndicator = DevelopmentIndicator.Create(OtherIndicatorId, DomainId, "Cleanliness", 2, DevelopmentIndicatorStatus.Active);
        var archivedIndicator = DevelopmentIndicator.Create(ArchivedIndicatorId, DomainId, "Retired indicator", 3, DevelopmentIndicatorStatus.Archived);
        var domain = DevelopmentDomain.Create(
            DomainId, SectionId, "Personal & Physical Development", 1, ScaleId, allowsIndicatorComment: true,
            DevelopmentDomainStatus.Active, [indicator, otherIndicator, archivedIndicator]);

        var noCommentIndicator = DevelopmentIndicator.Create(NoCommentIndicatorId, NoCommentDomainId, "Attendance to Class", 1, DevelopmentIndicatorStatus.Active);
        var noCommentDomain = DevelopmentDomain.Create(
            NoCommentDomainId, SectionId, "Social & Emotional", 2, ScaleId, allowsIndicatorComment: false,
            DevelopmentDomainStatus.Active, [noCommentIndicator]);

        _developmentDomains.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([domain, noCommentDomain]);

        var scale = RatingScale.Create(ScaleId, "Nursery development", [RatingScalePoint.Create(PointId, ScaleId, "E", "Excellent", 1)]);
        var otherScale = RatingScale.Create(OtherScaleId, "Primary trait", [RatingScalePoint.Create(WrongScalePointId, OtherScaleId, "E", "Excellent", 1)]);
        _ratingScales.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([scale, otherScale]);

        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
        _developmentRatings.ListTrackedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private SaveDevelopmentRatingsHandler CreateHandler() => new(
        _arms, _sessions, _terms, _classLevels, _sections, _enrolments, _developmentDomains, _ratingScales, _resultSets,
        _developmentRatings, _currentUser, _auditSink);

    private static SaveDevelopmentRatingsCommand Command(string? version, params SaveDevelopmentRatingsRowInput[] rows) =>
        new(ArmId.ToString(), TermId.ToString(), version, rows);

    private static Dictionary<string, DevelopmentRatingCellDto?> Ratings(params (Guid IndicatorId, DevelopmentRatingCellDto? Cell)[] cells) =>
        cells.ToDictionary(entry => entry.IndicatorId.ToString(), entry => entry.Cell);

    private static string Id(Guid value) => value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task HandleAsync_WhenTheArmsSectionHasNoActiveDevelopmentDomain_Returns422()
    {
        _developmentDomains.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler().HandleAsync(Command(null), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GetDevelopmentRatingsHandler.SectionNotRatedErrorCode);
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
        result.Error.Code.ShouldBe("development_ratings.session_closed");
    }

    [Fact]
    public async Task HandleAsync_APupilNotOnTheRoster_Returns422()
    {
        var row = new SaveDevelopmentRatingsRowInput(Guid.CreateVersion7().ToString(), new Dictionary<string, DevelopmentRatingCellDto?>());

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.ShouldContainKey("Rows[0].PupilId");
    }

    [Fact]
    public async Task HandleAsync_AnUnknownIndicatorId_Returns422()
    {
        var unknownIndicatorId = Guid.CreateVersion7();
        var row = new SaveDevelopmentRatingsRowInput(PupilId.ToString(), Ratings((unknownIndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("This is not a known indicator.");
    }

    [Fact]
    public async Task HandleAsync_AnArchivedIndicatorId_Returns422()
    {
        var row = new SaveDevelopmentRatingsRowInput(PupilId.ToString(), Ratings((ArchivedIndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("This indicator is archived and cannot be rated.");
    }

    [Fact]
    public async Task HandleAsync_APointFromTheWrongScale_Returns422()
    {
        var row = new SaveDevelopmentRatingsRowInput(PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(WrongScalePointId), null))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("This point is not on Nursery development.");
    }

    [Fact]
    public async Task HandleAsync_ACommentWithANullPoint_Returns422()
    {
        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(null, "Needs reminding."))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("A comment requires a rating. Choose a point, or clear the comment too.");
    }

    [Fact]
    public async Task HandleAsync_ACommentOnADomainThatDisallowsComments_Returns422()
    {
        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((NoCommentIndicatorId, new DevelopmentRatingCellDto(Id(PointId), "A comment."))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("Social & Emotional does not allow a comment on its indicators.");
    }

    [Fact]
    public async Task HandleAsync_ACommentOver120Characters_Returns422()
    {
        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), new string('a', DevelopmentRating.CommentMaxLength + 1)))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<SchoolManagement.Domain.Common.ValidationError>();
        error.Failures.Values.SelectMany(v => v).ShouldContain("Comment must be 120 characters or fewer.");
    }

    [Fact]
    public async Task HandleAsync_FirstRatingForTheArm_CreatesTheResultSetInDraft()
    {
        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), "Needs reminding."))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ResultSet.ShouldNotBeNull();
        result.Value.ResultSet!.State.ShouldBe(ResultSetState.Draft);
        result.Value.ResultSet!.NeedsRecompute.ShouldBeTrue();
        await _resultSets.Received(1).AddAsync(Arg.Any<ResultSet>(), Arg.Any<CancellationToken>());
        await _developmentRatings.Received(1).AddAsync(Arg.Any<DevelopmentRating>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OmittedIndicatorKey_LeavesTheExistingRatingUntouched()
    {
        // Q1-A ruling: a key absent from Ratings leaves that indicator's existing rating alone.
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);

        var existingRating = DevelopmentRating.Create(Guid.CreateVersion7(), resultSet.Id, PupilId, OtherIndicatorId, PointId, "Existing comment.").Value;
        _developmentRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingRating]);

        var currentVersion = DevelopmentRatingVersion.Compute([new DevelopmentRatingSnapshot(PupilId, OtherIndicatorId, PointId, "Existing comment.")]);

        // Save touches ONLY IndicatorId, never mentioning OtherIndicatorId.
        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var otherCell = result.Value.Rows.Single().Ratings[Id(OtherIndicatorId)];
        otherCell.PointId.ShouldBe(Id(PointId));
        otherCell.Comment.ShouldBe("Existing comment.");
        await _developmentRatings.DidNotReceive().RemoveAsync(Arg.Any<DevelopmentRating>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ExplicitNullClearsTheExistingRating()
    {
        // Q1-A ruling: a key present with an explicit null deletes that indicator's rating.
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);

        var existingRating = DevelopmentRating.Create(Guid.CreateVersion7(), resultSet.Id, PupilId, IndicatorId, PointId, "Existing comment.").Value;
        _developmentRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingRating]);

        var currentVersion = DevelopmentRatingVersion.Compute([new DevelopmentRatingSnapshot(PupilId, IndicatorId, PointId, "Existing comment.")]);

        var row = new SaveDevelopmentRatingsRowInput(PupilId.ToString(), Ratings((IndicatorId, null)));

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var cell = result.Value.Rows.Single().Ratings[Id(IndicatorId)];
        cell.PointId.ShouldBeNull();
        cell.Comment.ShouldBeNull();
        await _developmentRatings.Received(1).RemoveAsync(existingRating, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ClearingThePoint_AlsoClearsTheComment()
    {
        // Q3-A: "clearing the point clears the comment" — sending {pointId: null} (no comment
        // riding along) is a second, equivalent way to express the same clear Q1-A's explicit-null
        // value expresses.
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);

        var existingRating = DevelopmentRating.Create(Guid.CreateVersion7(), resultSet.Id, PupilId, IndicatorId, PointId, "Existing comment.").Value;
        _developmentRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([existingRating]);

        var currentVersion = DevelopmentRatingVersion.Compute([new DevelopmentRatingSnapshot(PupilId, IndicatorId, PointId, "Existing comment.")]);

        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(null, null))));

        var result = await CreateHandler().HandleAsync(Command(currentVersion, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var cell = result.Value.Rows.Single().Ratings[Id(IndicatorId)];
        cell.PointId.ShouldBeNull();
        cell.Comment.ShouldBeNull();
        await _developmentRatings.Received(1).RemoveAsync(existingRating, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _developmentRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);

        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command("a-stale-hash", row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveDevelopmentRatingsHandler.StaleVersionErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResultSetIsApproved_Returns409Locked()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), ArmId, TermId).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, ResultSetState.Approved);
        _resultSets.FindTrackedByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns(resultSet);
        _developmentRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);

        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SaveDevelopmentRatingsHandler.ResultSetLockedErrorCode);
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
        _developmentRatings.ListTrackedAsync(resultSet.Id, Arg.Any<CancellationToken>()).Returns([]);

        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        resultSet.NeedsRecompute.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_OnASuccessfulSave_RecordsAnAuditEvent()
    {
        var row = new SaveDevelopmentRatingsRowInput(
            PupilId.ToString(), Ratings((IndicatorId, new DevelopmentRatingCellDto(Id(PointId), null))));

        var result = await CreateHandler().HandleAsync(Command(null, row), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _auditSink.Received(1).RecordAsync(
            Arg.Is(Privileges.Results.TraitEnter), Arg.Is("development_rating"), Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>>());
    }
}
