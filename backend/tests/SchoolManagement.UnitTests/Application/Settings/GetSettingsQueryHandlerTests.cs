using NSubstitute;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="GetSettingsQueryHandler"/>.</summary>
public sealed class GetSettingsQueryHandlerTests
{
    private readonly ISchoolProfileRepository _repository = Substitute.For<ISchoolProfileRepository>();
    private readonly IPupilRepository _pupils = Substitute.For<IPupilRepository>();

    private GetSettingsQueryHandler CreateHandler() => new(_repository, _pupils);

    [Fact]
    public async Task HandleAsync_ReturnsTheIdentityGroupMappedFromTheProfile()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            schoolName: "Golden Royal Ark School",
            identityVersionNumber: 3);
        _repository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await CreateHandler().HandleAsync(new GetSettingsQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Identity.SchoolName.ShouldBe("Golden Royal Ark School");
        result.Value.Identity.VersionNumber.ShouldBe(3);
        result.Value.Identity.Timezone.ShouldBe(SchoolProfile.FixedTimezone);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheAbbreviationGroupWithARealIssuedCount()
    {
        // TASK-0051: the register is real now — a live count read from the pupil table, never null.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviation: "GRAS");
        _repository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _pupils.CountByRegistrationNumberPrefixAsync("GRAS", Arg.Any<CancellationToken>()).Returns(7);

        var result = await CreateHandler().HandleAsync(new GetSettingsQuery(), TestContext.Current.CancellationToken);

        result.Value.Abbreviation.Abbreviation.ShouldBe("GRAS");
        result.Value.Abbreviation.IssuedCount.ShouldBe(7);
    }

    [Fact]
    public async Task HandleAsync_WithNoPupilsIssuedUnderTheAbbreviation_ReturnsZeroNeverNull()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviation: "GRAS");
        _repository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);
        _pupils.CountByRegistrationNumberPrefixAsync("GRAS", Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler().HandleAsync(new GetSettingsQuery(), TestContext.Current.CancellationToken);

        result.Value.Abbreviation.IssuedCount.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheRegNumberGroupWithTheFixedYearSource()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        _repository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await CreateHandler().HandleAsync(new GetSettingsQuery(), TestContext.Current.CancellationToken);

        result.Value.RegNumber.Separator.ShouldBe(SchoolProfile.DefaultSeparator);
        result.Value.RegNumber.SerialWidth.ShouldBe(SchoolProfile.DefaultSerialWidth);
        result.Value.RegNumber.SerialReset.ShouldBe(SchoolProfile.DefaultSerialReset);
        result.Value.RegNumber.YearSource.ShouldBe(SchoolProfile.YearSource);
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
