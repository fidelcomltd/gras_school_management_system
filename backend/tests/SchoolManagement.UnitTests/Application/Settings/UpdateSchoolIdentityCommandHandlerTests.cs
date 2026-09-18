using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// <see cref="UpdateSchoolIdentityCommandHandler"/> — TASK-0005a's central acceptance criteria:
/// optimistic concurrency rejects a stale save before anything is written, and BOTH the winning and
/// the losing attempt are recorded on the audit trail (spec 6.2.11), while only the winner ever
/// produces a <see cref="ConfigVersion"/> row (the append-only guarantee).
/// </summary>
public sealed class UpdateSchoolIdentityCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();
    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly ISettingsSnapshotSource _settingsSnapshotSource = Substitute.For<ISettingsSnapshotSource>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    // TASK-0069/TASK-0077/TASK-0072: the snapshot now reads the current grading/assessment/result-rules/
    // rating-scales/development-domains state even from a save that does not touch any of them — stub
    // the bundled source so SettingsSnapshotBuilder.Build never sees null.
    private UpdateSchoolIdentityCommandHandler CreateHandler()
    {
        _settingsSnapshotSource.LoadAsync(Arg.Any<CancellationToken>()).Returns(new SettingsSnapshotState(
            Array.Empty<GradingBand>(),
            Array.Empty<AssessmentComponent>(),
            ResultRules.CreateSeed(Guid.CreateVersion7()),
            Array.Empty<RatingScale>(),
            Array.Empty<DevelopmentDomain>()));

        return new(
            _schoolProfileRepository,
            _configVersionRepository,
            _settingsSnapshotSource,
            _currentUser,
            _auditSink,
            _timeProvider);
    }

    private static UpdateSchoolIdentityCommand ValidCommand(int expectedVersion) => new(
        "Golden Royal Ark School",
        "GRAS",
        "12 Ark Crescent",
        "08012345678",
        "info@example.com",
        "Excellence Through Character",
        "Chisom Maxwell",
        expectedVersion);

    [Fact]
    public async Task HandleAsync_WithTheCurrentVersion_UpdatesTheProfileAndAppendsOneVersionRow()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), identityVersionNumber: 2);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _currentUser.UserId.Returns("admin-1");

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 2),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SchoolName.ShouldBe("Golden Royal Ark School");
        result.Value.VersionNumber.ShouldBe(3);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => IsTheExpectedWinningVersion(version, "admin-1")),
            Arg.Any<CancellationToken>());

        await _auditSink.Received(1).RecordAsync(
            "settings.identity.updated",
            Arg.Any<string>(),
            Arg.Any<string>(),
            metadata: null,
            actorAdminId: "admin-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409_WritesNoVersionRow_ButDoesAudit()
    {
        // The heart of spec 6.2.11: "both attempts appear in the audit log" — not "both attempts get
        // a version row." The loser here must reach the audit sink and stop there.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), identityVersionNumber: 5);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _currentUser.UserId.Returns("admin-2");

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 4), // Stale: the profile is already at 5.
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateSchoolIdentityCommandHandler.StaleVersionErrorCode);
        result.Error.Description.ShouldNotContain("grading scale");

        await _configVersionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);

        // RecordRejectionAsync, not RecordAsync (TASK-0048): this row must survive the ambient
        // transaction's rollback, which a same-transaction write would not.
        await _auditSink.Received(1).RecordRejectionAsync(
            "settings.identity.save_rejected_stale_version",
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
            schoolName: "Original Name",
            identityVersionNumber: 5);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        await CreateHandler().HandleAsync(ValidCommand(expectedVersion: 4), TestContext.Current.CancellationToken);

        // Proves the loser never reaches SchoolProfile.UpdateIdentity at all, not merely that its
        // write gets rolled back later.
        profile.SchoolName.ShouldBe("Original Name");
        profile.IdentityVersionNumber.ShouldBe(5);
    }

    [Fact]
    public async Task HandleAsync_TwoSavesAgainstTheSameStartingVersion_OnlyTheFirstWins()
    {
        // Drives two saves against the same version, as the acceptance criterion requires: the first
        // (this handler instance, reused against the same in-memory profile — standing in for "the
        // first transaction to commit") succeeds and bumps the pointer; the second, replaying the
        // now-stale value a second admin would have read before either save happened, is rejected.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), identityVersionNumber: 1);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var firstAttempt = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 1),
            TestContext.Current.CancellationToken);

        var secondAttempt = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 1), // The same value both admins read before either saved.
            TestContext.Current.CancellationToken);

        firstAttempt.IsSuccess.ShouldBeTrue();
        secondAttempt.IsFailure.ShouldBeTrue();
        secondAttempt.Error.Code.ShouldBe(UpdateSchoolIdentityCommandHandler.StaleVersionErrorCode);

        // Exactly one version row for two attempts — the append-only guarantee under a real race.
        await _configVersionRepository.Received(1).AddAsync(Arg.Any<ConfigVersion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }

    // A plain method call, not an inline lambda body: matches IdempotencyPurgeJobTests.HasCount's
    // reasoning — Arg.Is<T>'s predicate parameter is nullable-annotated by NSubstitute even though it
    // is never actually null in practice, so the null check here satisfies the analyser rather than
    // suppressing it.
    private static bool IsTheExpectedWinningVersion(ConfigVersion? version, string expectedActorAdminId) =>
        version is not null &&
        version.ChangedGroup == ConfigVersionGroup.Identity &&
        version.ActorAdminId == expectedActorAdminId &&
        version.Reason is null &&
        version.SnapshotJson.Contains("Golden Royal Ark School", StringComparison.Ordinal);
}
