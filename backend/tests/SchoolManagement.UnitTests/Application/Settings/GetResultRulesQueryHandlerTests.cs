using NSubstitute;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="GetResultRulesQueryHandler"/>.</summary>
public sealed class GetResultRulesQueryHandlerTests
{
    private readonly IResultRulesRepository _resultRulesRepository = Substitute.For<IResultRulesRepository>();
    private readonly ISchoolProfileRepository _schoolProfileRepository = Substitute.For<ISchoolProfileRepository>();

    private GetResultRulesQueryHandler CreateHandler() => new(_resultRulesRepository, _schoolProfileRepository);

    [Fact]
    public async Task HandleAsync_ReturnsTheSeededDefaults()
    {
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(resultRules);
        _schoolProfileRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>())
            .Returns(SchoolProfile.CreateForTesting(Guid.CreateVersion7(), resultRulesVersionNumber: 0));

        var result = await CreateHandler().HandleAsync(new GetResultRulesQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AnnualMethod.ShouldBe(AnnualMethod.SimpleAverage);
        result.Value.PrimaryPositionScope.ShouldBe(PrimaryPositionScope.Arm);
        result.Value.ShowLevelPosition.ShouldBeTrue();
        result.Value.TieBreakRule.ShouldBe(TieBreakRule.SharedPosition);
        result.Value.PassMark.ShouldBe(40);
        result.Value.PromotionThreshold.ShouldBe(40);
        result.Value.RequireCorePass.ShouldBeTrue();
        result.Value.CoreSubjectIds.ShouldBeEmpty();
        result.Value.MinSubjectsForPosition.ShouldBe(1);
        result.Value.VersionNumber.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheProfilesResultRulesVersionNumber()
    {
        var resultRules = ResultRules.CreateSeed(Guid.CreateVersion7());
        _resultRulesRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(resultRules);
        _schoolProfileRepository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>())
            .Returns(SchoolProfile.CreateForTesting(Guid.CreateVersion7(), resultRulesVersionNumber: 7));

        var result = await CreateHandler().HandleAsync(new GetResultRulesQuery(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.VersionNumber.ShouldBe(7);
    }
}
