using SchoolManagement.Application.Security.PrivilegeRegister;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Application.Security.PrivilegeRegister;

/// <summary>Unit tests for <see cref="GetPrivilegeRegisterQueryHandler"/>.</summary>
public sealed class GetPrivilegeRegisterQueryHandlerTests
{
    // A plain class with no dependencies: constructed directly, per PingQueryHandlerTests' template.
    private static GetPrivilegeRegisterQueryHandler CreateHandler() => new();

    [Fact]
    public async Task HandleAsync_ReturnsSuccess()
    {
        var result = await CreateHandler().HandleAsync(
            new GetPrivilegeRegisterQuery(),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ReturnsExactlySixGroupsInSpecOrder()
    {
        var result = await CreateHandler().HandleAsync(
            new GetPrivilegeRegisterQuery(),
            TestContext.Current.CancellationToken);

        result.Value.Groups.Select(group => group.Key).ShouldBe(
        [
            "administration",
            "settings",
            "academic_structure",
            "pupils_and_subjects",
            "results",
            "pins_and_reports",
        ]);
    }

    [Fact]
    public async Task HandleAsync_ReturnsAllNinetyThreePrivilegesAcrossTheGroups()
    {
        var result = await CreateHandler().HandleAsync(
            new GetPrivilegeRegisterQuery(),
            TestContext.Current.CancellationToken);

        result.Value.Groups.Sum(group => group.Privileges.Count).ShouldBe(PrivilegeRegistry.All.Count);
    }

    [Fact]
    public async Task HandleAsync_TitlesAreTheVerbatimSpecHeadings()
    {
        var result = await CreateHandler().HandleAsync(
            new GetPrivilegeRegisterQuery(),
            TestContext.Current.CancellationToken);

        var academicStructure = result.Value.Groups.Single(group => group.Key == "academic_structure");
        academicStructure.Title.ShouldBe("Academic structure");

        var pupilsAndSubjects = result.Value.Groups.Single(group => group.Key == "pupils_and_subjects");
        pupilsAndSubjects.Title.ShouldBe("Pupils, guardians and subjects");
    }

    [Fact]
    public async Task HandleAsync_NoGuardianAliasAppearsAnywhereInTheResponse()
    {
        var result = await CreateHandler().HandleAsync(
            new GetPrivilegeRegisterQuery(),
            TestContext.Current.CancellationToken);

        var codes = result.Value.Groups.SelectMany(group => group.Privileges).Select(privilege => privilege.Code);

        codes.ShouldAllBe(code => !code.StartsWith("guardian.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_EachDescriptorMatchesItsRegistryDefinitionExactly()
    {
        var result = await CreateHandler().HandleAsync(
            new GetPrivilegeRegisterQuery(),
            TestContext.Current.CancellationToken);

        var descriptorsByCode = result.Value.Groups
            .SelectMany(group => group.Privileges)
            .ToDictionary(descriptor => descriptor.Code);

        foreach (var definition in PrivilegeRegistry.All)
        {
            var descriptor = descriptorsByCode[definition.Code];
            descriptor.Permits.ShouldBe(definition.Permits);
            descriptor.Scopable.ShouldBe(definition.Scopable);
        }
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
