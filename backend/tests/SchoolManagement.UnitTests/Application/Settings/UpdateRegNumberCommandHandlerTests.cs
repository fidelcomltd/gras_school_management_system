using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="UpdateRegNumberCommandHandler"/>: optimistic concurrency (mirroring
/// <c>UpdateSchoolIdentityCommandHandler</c>'s own proven pattern) and spec 6.2.10's width-reduction
/// rejection, including the amendment-1 requirement that the check reads the partition the group's
/// CURRENTLY SAVED <c>serialReset</c> selects, not whichever mode the request is trying to switch to.
/// </summary>
public sealed class UpdateRegNumberCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();

    private readonly IRegistrationCounterRepository _registrationCounterRepository =
        Substitute.For<IRegistrationCounterRepository>();

    private readonly IConfigVersionRepository _configVersionRepository = Substitute.For<IConfigVersionRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();

    private readonly IAssessmentComponentRepository _assessmentComponentRepository =
        Substitute.For<IAssessmentComponentRepository>();

    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    // TASK-0069/TASK-0077: the snapshot now reads the current grading/assessment/result-rules state
    // even from a save that does not touch any of them — stub all three so SettingsSnapshotBuilder.Build
    // never sees null.
    private UpdateRegNumberCommandHandler CreateHandler()
    {
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingBand>());
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AssessmentComponent>());
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(ResultRules.CreateSeed(Guid.CreateVersion7()));

        return new(
            _schoolProfileRepository,
            _registrationCounterRepository,
            _configVersionRepository,
            _gradingBandRepository,
            _assessmentComponentRepository,
            _resultRulesRepository,
            _currentUser,
            _auditSink,
            _timeProvider);
    }

    private static UpdateRegNumberCommand ValidCommand(int expectedVersion, int serialWidth = 4) => new(
        "/",
        serialWidth,
        RegNumberSerialReset.PerYear,
        expectedVersion);

    [Fact]
    public async Task HandleAsync_WithTheCurrentVersion_AndNoIssuedSerial_UpdatesTheProfileAndAppendsOneVersionRow()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), regNumberVersionNumber: 2);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        _currentUser.UserId.Returns("admin-1");

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 2) with { SerialReset = RegNumberSerialReset.Continuous },
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SerialReset.ShouldBe(RegNumberSerialReset.Continuous);
        result.Value.VersionNumber.ShouldBe(3);

        await _configVersionRepository.Received(1).AddAsync(
            Arg.Is<ConfigVersion>(version => IsTheExpectedWinningVersion(version, "admin-1")),
            Arg.Any<CancellationToken>());

        await _auditSink.Received(1).RecordAsync(
            "settings.regnumber.updated",
            Arg.Any<string>(),
            Arg.Any<string>(),
            metadata: null,
            actorAdminId: "admin-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAStaleVersion_Returns409_WritesNoVersionRow_ButDoesAudit()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), regNumberVersionNumber: 5);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _currentUser.UserId.Returns("admin-2");

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 4),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRegNumberCommandHandler.StaleVersionErrorCode);
        result.Error.Description.ShouldNotContain("grading scale");

        await _configVersionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        await _registrationCounterRepository.DidNotReceiveWithAnyArgs()
            .GetLastSerialAsync(default!, TestContext.Current.CancellationToken);

        await _auditSink.Received(1).RecordRejectionAsync(
            "settings.regnumber.save_rejected_stale_version",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            actorAdminId: "admin-2",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WidthTooSmallForTheActivePartition_Returns409NamingTheRealSerialAndTheMinimumWidth()
    {
        // Spec 6.2.10's exact worked example.
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            regNumberVersionNumber: 0,
            serialReset: RegNumberSerialReset.PerYear);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync("2026", Arg.Any<CancellationToken>()).Returns(1043);

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 0, serialWidth: 3),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRegNumberCommandHandler.WidthTooSmallErrorCode);
        result.Error.Description.ShouldBe("Serial 1043 will not fit in a width of 3. Choose 4 or more.");

        await _configVersionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        profile.SerialWidth.ShouldBe(SchoolProfile.DefaultSerialWidth); // Never mutated.
    }

    [Fact]
    public async Task HandleAsync_WidthCheckReadsThePartitionForTheCurrentlySavedSerialReset_NotTheIncomingOne()
    {
        // Amendment 1's other half: the profile is CURRENTLY per_year (so "2026" is the active
        // partition right now), even though this request tries to switch it to continuous. The
        // rejection must still be based on "2026"'s real data, not "ALL"'s.
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            regNumberVersionNumber: 0,
            serialReset: RegNumberSerialReset.PerYear);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync("2026", Arg.Any<CancellationToken>()).Returns(1043);
        _registrationCounterRepository
            .GetLastSerialAsync(RegistrationCounterPartition.ContinuousKey, Arg.Any<CancellationToken>())
            .Returns(0); // If the handler wrongly read this partition instead, the save would succeed.

        var result = await CreateHandler().HandleAsync(
            new UpdateRegNumberCommand("/", 3, RegNumberSerialReset.Continuous, ExpectedVersion: 0),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpdateRegNumberCommandHandler.WidthTooSmallErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WidthExactlySufficient_AllowsTheSave()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), regNumberVersionNumber: 0);
        _schoolProfileRepository.GetTrackedSingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync("2026", Arg.Any<CancellationToken>()).Returns(999);

        var result = await CreateHandler().HandleAsync(
            ValidCommand(expectedVersion: 0, serialWidth: 3), // 999 needs exactly 3 digits.
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }

    // A plain method call, not an inline lambda body: matches UpdateSchoolIdentityCommandHandlerTests'
    // own reasoning — Arg.Is<T>'s predicate parameter is nullable-annotated by NSubstitute even though
    // it is never actually null in practice, so the null check here satisfies the analyser rather than
    // suppressing it.
    private static bool IsTheExpectedWinningVersion(ConfigVersion? version, string expectedActorAdminId) =>
        version is not null &&
        version.ChangedGroup == ConfigVersionGroup.RegistrationNumber &&
        version.ActorAdminId == expectedActorAdminId &&
        version.Reason is null;
}
