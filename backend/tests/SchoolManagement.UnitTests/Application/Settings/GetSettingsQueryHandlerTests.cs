using NSubstitute;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="GetSettingsQueryHandler"/>.</summary>
public sealed class GetSettingsQueryHandlerTests
{
    private readonly ISchoolProfileRepository _repository = Substitute.For<ISchoolProfileRepository>();

    private GetSettingsQueryHandler CreateHandler() => new(_repository);

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
    public async Task HandleAsync_ReturnsTheAbbreviationGroupWithANullIssuedCount()
    {
        // Amendment 2: no pupil register exists yet, so this must always be null, never 0.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviation: "GRAS");
        _repository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await CreateHandler().HandleAsync(new GetSettingsQuery(), TestContext.Current.CancellationToken);

        result.Value.Abbreviation.Abbreviation.ShouldBe("GRAS");
        result.Value.Abbreviation.IssuedCount.ShouldBeNull();
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
