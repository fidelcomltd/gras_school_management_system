using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>
/// Tests <see cref="GetRegNumberPreviewQueryHandler"/> — in particular the card's central acceptance
/// criterion (approved delta amendment 1): under <c>continuous</c> the serial does not restart across
/// a year boundary; under <c>per_year</c> it does. Both directions are proven here, or the criterion
/// is unproven.
/// </summary>
public sealed class GetRegNumberPreviewQueryHandlerTests
{
    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();

    private readonly IRegistrationCounterRepository _registrationCounterRepository =
        Substitute.For<IRegistrationCounterRepository>();

    private GetRegNumberPreviewQueryHandler CreateHandler(DateTimeOffset now) =>
        new(_schoolProfileRepository, _registrationCounterRepository, new FakeTimeProvider(now));

    [Fact]
    public async Task HandleAsync_UnderContinuous_CrossingAYearBoundary_TheSerialContinuesRatherThanRestarting()
    {
        // Amendment 1's central proof. Seeded counter state: the "ALL" partition already holds 41
        // issued serials carried over from a PREVIOUS year — the only partition `continuous` ever
        // reads.
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            serialReset: RegNumberSerialReset.Continuous);
        _schoolProfileRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository
            .GetLastSerialAsync(RegistrationCounterPartition.ContinuousKey, Arg.Any<CancellationToken>())
            .Returns(41);
        // A year-keyed row also exists, but must NEVER be read under `continuous` — reading it instead
        // would produce a different (wrong) result than the assertion below expects.
        _registrationCounterRepository.GetLastSerialAsync("2027", Arg.Any<CancellationToken>()).Returns(0);

        var resultInLateDecember2026 = await CreateHandler(new DateTimeOffset(2026, 12, 20, 0, 0, 0, TimeSpan.Zero))
            .HandleAsync(new GetRegNumberPreviewQuery("/", 4), TestContext.Current.CancellationToken);
        var resultInEarlyJanuary2027 = await CreateHandler(new DateTimeOffset(2027, 1, 5, 0, 0, 0, TimeSpan.Zero))
            .HandleAsync(new GetRegNumberPreviewQuery("/", 4), TestContext.Current.CancellationToken);

        resultInLateDecember2026.Value.Preview.ShouldBe("GRAS/2026/0042");
        // The year segment changes (2026 -> 2027, spec 6.5.10: the number still records the real
        // calendar year), but the SERIAL does not restart at 1 — it continues from the same running
        // counter.
        resultInEarlyJanuary2027.Value.Preview.ShouldBe("GRAS/2027/0042");
    }

    [Fact]
    public async Task HandleAsync_UnderPerYear_CrossingAYearBoundary_TheSerialDoesRestart()
    {
        // The other required direction (the card fails without both): per_year genuinely resets. The
        // 2026 partition holds 39 issued serials; the 2027 partition has never been touched.
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            serialReset: RegNumberSerialReset.PerYear);
        _schoolProfileRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync("2026", Arg.Any<CancellationToken>()).Returns(39);
        _registrationCounterRepository.GetLastSerialAsync("2027", Arg.Any<CancellationToken>()).Returns(0);

        var resultIn2026 = await CreateHandler(new DateTimeOffset(2026, 12, 20, 0, 0, 0, TimeSpan.Zero))
            .HandleAsync(new GetRegNumberPreviewQuery("/", 4), TestContext.Current.CancellationToken);
        var resultIn2027 = await CreateHandler(new DateTimeOffset(2027, 1, 5, 0, 0, 0, TimeSpan.Zero))
            .HandleAsync(new GetRegNumberPreviewQuery("/", 4), TestContext.Current.CancellationToken);

        resultIn2026.Value.Preview.ShouldBe("GRAS/2026/0040");
        resultIn2027.Value.Preview.ShouldBe("GRAS/2027/0001"); // Restarted, unlike continuous above.
    }

    [Fact]
    public async Task HandleAsync_WithAnEmptyRegister_TheFirstSerialIsOne()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviation: "GRAS");
        _schoolProfileRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero))
            .HandleAsync(new GetRegNumberPreviewQuery("/", 4), TestContext.Current.CancellationToken);

        result.Value.Preview.ShouldBe("GRAS/2026/0001");
    }

    [Fact]
    public async Task HandleAsync_UsesTheUnsavedSeparatorAndWidthFromTheQuery_NotTheSavedProfile()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            separator: "/",
            serialWidth: 4);
        _schoolProfileRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _registrationCounterRepository.GetLastSerialAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero))
            .HandleAsync(new GetRegNumberPreviewQuery("-", 6), TestContext.Current.CancellationToken);

        result.Value.Preview.ShouldBe("GRAS-2026-000001"); // Neither "/" nor width 4 — those are saved, not supplied.
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler(DateTimeOffset.UtcNow).HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
