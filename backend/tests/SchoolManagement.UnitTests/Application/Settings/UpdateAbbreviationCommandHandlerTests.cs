using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="UpdateAbbreviationCommandHandler"/>: optimistic concurrency (mirroring
/// <c>UpdateSchoolIdentityCommandHandler</c>'s own proven pattern), that a historically-used value is
/// accepted without complaint (spec 6.2.11), and that the reason is persisted onto the
/// <see cref="ConfigVersion"/> row (unlike the identity group's own, which is always <see langword="null"/>).
/// </summary>
public sealed class UpdateAbbreviationCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();

    private readonly IAssessmentComponentRepository _assessmentComponentRepository =
        Substitute.For<IAssessmentComponentRepository>();

    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IPupilRepository _pupils = Substitute.For<IPupilRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    // TASK-0069/TASK-0077/TASK-0072: the snapshot now reads the current grading/assessment/result-rules/
    // rating-scales state even from a save that does not touch any of them — stub all four so
    // SettingsSnapshotBuilder.Build never sees null.
    private UpdateAbbreviationCommandHandler CreateHandler()
    {
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingBand>());
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AssessmentComponent>());
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(ResultRules.CreateSeed(Guid.CreateVersion7()));
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());

        return new(
            _schoolProfileRepository,
            _configVersionRepository,
            _gradingBandRepository,
            _assessmentComponentRepository,
            _resultRulesRepository,
            _ratingScaleRepository,
            _pupils,
            _currentUser,
            _auditSink,
            _timeProvider);
    }

    private static UpdateAbbreviationCommand ValidCommand(int expectedVersion, string abbreviation = "GRA") => new(
        abbreviation,
        UpdateAbbreviationCommandValidator.RequiredConfirmationToken,
        "The school shortened its registered trading name.",
        expectedVersion);

    [Fact]
    public async Task HandleAsync_WithTheCurrentVersion_UpdatesTheProfileAndAppendsOneVersionRowCarryingTheReason()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            abbreviationVersionNumber: 2);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _currentUser.UserId.Returns("admin-1");
        // TASK-0051: a live count against the NEW abbreviation, never null.
        _pupils.CountByRegistrationNumberPrefixAsync("GRA", Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 2, abbreviation: "GRA"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Abbreviation.ShouldBe("GRA");
        result.Value.IssuedCount.ShouldBe(0);
        result.Value.VersionNumber.ShouldBe(3);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => IsTheExpectedWinningVersion(version, "admin-1")),
            Arg.Any<CancellationToken>());

        await _auditSink.Received(1).RecordAsync(
            "settings.abbreviation.updated",
            Arg.Any<string>(),
            Arg.Any<string>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, object?>>(),
            actorAdminId: "admin-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TrimsTheReasonBeforeStoringIt()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviationVersionNumber: 0);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 0) with { Reason = "  Trimmed reason.  " },
            TestContext.Current.CancellationToken);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => HasReason(version, "Trimmed reason.")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ToAValueAlreadyUsedHistorically_IsAllowed()
    {
        // Spec 6.2.11: "Allowed. Abbreviations are not unique over time and a school returning to a
        // previous prefix is legitimate." This handler has no uniqueness check to trip.
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            abbreviationVersionNumber: 0);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var backToOriginal = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 0, abbreviation: "GRAS"), // Same value it already holds.
            TestContext.Current.CancellationToken);

        backToOriginal.IsSuccess.ShouldBeTrue();
        backToOriginal.Value.Abbreviation.ShouldBe("GRAS");
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409_WritesNoVersionRow_ButDoesAudit()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviationVersionNumber: 5);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _currentUser.UserId.Returns("admin-2");

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 4),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateAbbreviationCommandHandler.StaleVersionErrorCode);
        result.Error.Description.ShouldNotContain("grading scale");

        await _configVersionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordRejectionAsync(
            "settings.abbreviation.save_rejected_stale_version",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            actorAdminId: "admin-2",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_NeverMutatesTheProfile()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            abbreviationVersionNumber: 5);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        await CreateHandler().HandleAsync(ValidCommand(expectedVersion: 4), TestContext.Current.CancellationToken);

        profile.Abbreviation.ShouldBe("GRAS");
        profile.AbbreviationVersionNumber.ShouldBe(5);
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }

    // Plain method calls, not inline lambda bodies: matches UpdateSchoolIdentityCommandHandlerTests'
    // own reasoning — Arg.Is<T>'s predicate parameter is nullable-annotated by NSubstitute even though
    // it is never actually null in practice, so the null check here satisfies the analyser rather than
    // suppressing it.
    private static bool IsTheExpectedWinningVersion(ConfigVersion? version, string expectedActorAdminId) =>
        version is not null &&
        version.ChangedGroup == ConfigVersionGroup.Abbreviation &&
        version.ActorAdminId == expectedActorAdminId &&
        version.Reason == "The school shortened its registered trading name." &&
        version.SnapshotJson.Contains("\"abbreviation\":\"GRA\"", StringComparison.Ordinal);

    private static bool HasReason(ConfigVersion? version, string expectedReason) =>
        version is not null && version.Reason == expectedReason;
}
