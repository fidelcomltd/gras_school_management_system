using NSubstitute;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Settings;

/// <summary>Tests <see cref="GetSettingsQueryHandler"/>.</summary>
public sealed class GetSettingsQueryHandlerTests
{
    private readonly ISchoolProfileRepository _repository = Substitute.For<ISchoolProfileRepository>();
    private readonly IPupilRepository _pupils = Substitute.For<IPupilRepository>();
    private readonly IGradingBandRepository _gradingBandRepository = Substitute.For<IGradingBandRepository>();

    private readonly IAssessmentComponentRepository _assessmentComponentRepository =
        Substitute.For<IAssessmentComponentRepository>();

    private readonly IRatingScaleRepository _ratingScaleRepository = Substitute.For<IRatingScaleRepository>();
    private readonly IDevelopmentDomainRepository _developmentDomainRepository = Substitute.For<IDevelopmentDomainRepository>();
    private readonly ISectionRepository _sectionRepository = Substitute.For<ISectionRepository>();

    // Defaults set in the CONSTRUCTOR (runs once before each test method, per xUnit's per-test
    // instance model) so a test's own .Returns() setup — configured inside the test method body,
    // necessarily AFTER construction — always wins. Setting these same defaults inside CreateHandler()
    // instead would run AFTER a test's own setup (CreateHandler() is called at the point of use, deep
    // in the test body) and silently clobber it back to empty — the exact bug this comment prevents
    // from being reintroduced.
    public GetSettingsQueryHandlerTests()
    {
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GradingBand>());
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<AssessmentComponent>());
        _ratingScaleRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<RatingScale>());
        _developmentDomainRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<DevelopmentDomain>());
        _sectionRepository.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Section>());
    }

    private GetSettingsQueryHandler CreateHandler() =>
        new(_repository, _pupils, _gradingBandRepository, _assessmentComponentRepository, _ratingScaleRepository,
            _developmentDomainRepository, _sectionRepository);

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
    public async Task HandleAsync_ReturnsGradingAndAssessmentAsOrderedArrays()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            gradingVersionNumber: 2,
            assessmentVersionNumber: 5);
        _repository.GetReadOnlySingletonAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var bandA = GradingBand.Create(Guid.CreateVersion7(), 50, 100, "P", "Pass", displayOrder: 1);
        var bandB = GradingBand.Create(Guid.CreateVersion7(), 0, 49, "F", "Fail", displayOrder: 2);
        _gradingBandRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([bandA, bandB]);

        // Deliberately NOT 40/60 — the durability requirement bans the literal 40 as a CA total even
        // in a test fixture, so this proves the mapping doesn't care what the maximum is.
        var ca = AssessmentComponent.Create(Guid.CreateVersion7(), "CA", "CA", 45, isExamination: false, displayOrder: 1);
        var exam = AssessmentComponent.Create(Guid.CreateVersion7(), "Exam", "EXAM", 55, isExamination: true, displayOrder: 2);
        _assessmentComponentRepository.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([ca, exam]);

        var result = await CreateHandler().HandleAsync(new GetSettingsQuery(), TestContext.Current.CancellationToken);

        result.Value.Grading.VersionNumber.ShouldBe(2);
        result.Value.Grading.Bands.Count.ShouldBe(2);
        result.Value.Grading.Bands[0].GradeLetter.ShouldBe("P");
        result.Value.Grading.Bands[1].GradeLetter.ShouldBe("F");

        result.Value.Assessment.VersionNumber.ShouldBe(5);
        result.Value.Assessment.Components.Count.ShouldBe(2);
        result.Value.Assessment.Components[0].Name.ShouldBe("CA");
        result.Value.Assessment.Components[1].IsExamination.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
