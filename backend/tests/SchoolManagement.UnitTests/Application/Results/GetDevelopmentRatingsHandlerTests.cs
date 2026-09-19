using NSubstitute;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>Tests <see cref="GetDevelopmentRatingsHandler"/>: ruling R1's section gate and active-domain/active-indicator-only rows.</summary>
public sealed class GetDevelopmentRatingsHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid SectionId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid DomainId = Guid.CreateVersion7();
    private static readonly Guid ArchivedDomainId = Guid.CreateVersion7();
    private static readonly Guid IndicatorId = Guid.CreateVersion7();
    private static readonly Guid ArchivedIndicatorId = Guid.CreateVersion7();
    private static readonly Guid ScaleId = Guid.CreateVersion7();
    private static readonly Guid PointId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IClassLevelRepository _classLevels = Substitute.For<IClassLevelRepository>();
    private readonly ISectionRepository _sections = Substitute.For<ISectionRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly IDevelopmentDomainRepository _developmentDomains = Substitute.For<IDevelopmentDomainRepository>();
    private readonly IRatingScaleRepository _ratingScales = Substitute.For<IRatingScaleRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly IDevelopmentRatingRepository _developmentRatings = Substitute.For<IDevelopmentRatingRepository>();

    public GetDevelopmentRatingsHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        var level = ClassLevel.Create(ClassLevelId, "Nursery 1", SectionId, 1, null).Value;
        _classLevels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([level]);

        _sections.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([Section.Create(SectionId, "Nursery").Value]);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        var activeIndicator = DevelopmentIndicator.Create(IndicatorId, DomainId, "Potty trained", 1, DevelopmentIndicatorStatus.Active);
        var archivedIndicator = DevelopmentIndicator.Create(ArchivedIndicatorId, DomainId, "Retired indicator", 2, DevelopmentIndicatorStatus.Archived);
        var activeDomain = DevelopmentDomain.Create(
            DomainId, SectionId, "Personal & Physical Development", 1, ScaleId, allowsIndicatorComment: true,
            DevelopmentDomainStatus.Active, [activeIndicator, archivedIndicator]);
        var archivedDomain = DevelopmentDomain.Create(
            ArchivedDomainId, SectionId, "Retired domain", 2, ScaleId, allowsIndicatorComment: true,
            DevelopmentDomainStatus.Archived, []);
        _developmentDomains.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([activeDomain, archivedDomain]);

        _ratingScales.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(
        [
            RatingScale.Create(ScaleId, "Nursery development", [RatingScalePoint.Create(PointId, ScaleId, "E", "Excellent", 1)]),
        ]);

        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
    }

    private GetDevelopmentRatingsHandler CreateHandler() => new(
        _arms, _terms, _classLevels, _sections, _enrolments, _developmentDomains, _ratingScales, _resultSets, _developmentRatings);

    [Fact]
    public async Task HandleAsync_WhenTheArmsSectionHasNoActiveDevelopmentDomain_Returns422()
    {
        _developmentDomains.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler().HandleAsync(new GetDevelopmentRatingsQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GetDevelopmentRatingsHandler.SectionNotRatedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithNoRatingsEnteredAtAll_ReturnsEveryActivePupilBlankAndOnlyActiveDomainsAndIndicators()
    {
        var result = await CreateHandler().HandleAsync(new GetDevelopmentRatingsQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBeNull();
        result.Value.ResultSet.ShouldBeNull();
        result.Value.Domains.Count.ShouldBe(1);
        result.Value.Domains.Select(domain => domain.Id).ShouldNotContain(ArchivedDomainId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        var domain = result.Value.Domains.Single();
        domain.ActiveIndicatorCount.ShouldBe(1);
        domain.Indicators.Select(indicator => indicator.Id).ShouldNotContain(ArchivedIndicatorId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));

        result.Value.ActiveIndicatorTotal.ShouldBe(1);

        var row = result.Value.Rows.Single();
        row.Ratings.Keys.ShouldNotContain(ArchivedIndicatorId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        var cell = row.Ratings[IndicatorId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)];
        cell.PointId.ShouldBeNull();
        cell.Comment.ShouldBeNull();
    }
}
