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

/// <summary>Tests <see cref="GetTraitRatingsHandler"/>: ruling R1's section gate and active-trait-only rows.</summary>
public sealed class GetTraitRatingsHandlerTests
{
    private static readonly Guid ArmId = Guid.CreateVersion7();
    private static readonly Guid TermId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid SectionId = Guid.CreateVersion7();
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid TraitId = Guid.CreateVersion7();
    private static readonly Guid ArchivedTraitId = Guid.CreateVersion7();
    private static readonly Guid AffectiveScaleId = Guid.CreateVersion7();
    private static readonly Guid PsychomotorScaleId = Guid.CreateVersion7();
    private static readonly Guid AffectivePointId = Guid.CreateVersion7();

    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IClassLevelRepository _classLevels = Substitute.For<IClassLevelRepository>();
    private readonly ISectionRepository _sections = Substitute.For<ISectionRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();
    private readonly ITraitRepository _traits = Substitute.For<ITraitRepository>();
    private readonly IRatingScaleRepository _ratingScales = Substitute.For<IRatingScaleRepository>();
    private readonly IResultSetRepository _resultSets = Substitute.For<IResultSetRepository>();
    private readonly ITraitRatingRepository _traitRatings = Substitute.For<ITraitRatingRepository>();

    public GetTraitRatingsHandlerTests()
    {
        var arm = Arm.Create(ArmId, ClassLevelId, SessionId, "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(ArmId, Arg.Any<CancellationToken>()).Returns(arm);

        var term = Term.Create(TermId, SessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(TermId, Arg.Any<CancellationToken>()).Returns(term);

        var level = ClassLevel.Create(ClassLevelId, "Primary 1", SectionId, 1, null).Value;
        _classLevels.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([level]);

        _sections.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([Section.Create(SectionId, "Primary", ratesTraits: true).Value]);

        _enrolments.ListActiveRosterByArmAsync(ArmId, Arg.Any<CancellationToken>())
            .Returns([new ArmRosterPupil(PupilId, "GRAS/2026/0001", "Okafor", "Chidera", null)]);

        _traits.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Trait.Create(TraitId, TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active),
            Trait.Create(ArchivedTraitId, TraitDomain.Affective, "Retired", 2, TraitStatus.Archived),
        ]);

        _traits.ListBlocksReadOnlyAsync(Arg.Any<CancellationToken>()).Returns(
        [
            TraitBlock.Create(TraitDomain.Affective, AffectiveScaleId),
            TraitBlock.Create(TraitDomain.Psychomotor, PsychomotorScaleId),
        ]);

        _ratingScales.ListReadOnlyOrderedAsync(Arg.Any<CancellationToken>()).Returns(
        [
            RatingScale.Create(AffectiveScaleId, "Primary trait", [RatingScalePoint.Create(AffectivePointId, AffectiveScaleId, "E", "Excellent", 1)]),
            RatingScale.Create(PsychomotorScaleId, "Other scale", [RatingScalePoint.Create(Guid.CreateVersion7(), PsychomotorScaleId, "S", "Sports", 1)]),
        ]);

        _resultSets.FindReadOnlyByArmTermAsync(ArmId, TermId, Arg.Any<CancellationToken>()).Returns((ResultSet?)null);
    }

    private GetTraitRatingsHandler CreateHandler() => new(
        _arms, _terms, _classLevels, _sections, _enrolments, _traits, _ratingScales, _resultSets, _traitRatings);

    [Fact]
    public async Task HandleAsync_WhenTheArmsSectionDoesNotRateTraits_Returns422()
    {
        _sections.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns([Section.Create(SectionId, "Nursery", ratesTraits: false).Value]);

        var result = await CreateHandler().HandleAsync(new GetTraitRatingsQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(GetTraitRatingsHandler.SectionNotRatedErrorCode);
    }

    [Fact]
    public async Task HandleAsync_WithNoRatingsEnteredAtAll_ReturnsEveryActivePupilBlankAndOnlyActiveTraits()
    {
        var result = await CreateHandler().HandleAsync(new GetTraitRatingsQuery(ArmId.ToString(), TermId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBeNull();
        result.Value.ResultSet.ShouldBeNull();
        var affectiveBlock = result.Value.Blocks.Single(block => block.Domain == TraitDomain.Affective);
        affectiveBlock.Traits.Select(trait => trait.Id).ShouldNotContain(ArchivedTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Rows.Single().Ratings.Keys.ShouldNotContain(ArchivedTraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture));
        result.Value.Rows.Single().Ratings[TraitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)].ShouldBeNull();
    }
}
