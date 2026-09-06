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
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
