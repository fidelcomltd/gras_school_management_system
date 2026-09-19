using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>Entity-local invariants for <see cref="TraitRating"/> (TASK-0083 stage 1).</summary>
public sealed class TraitRatingTests
{
    [Fact]
    public void Create_WithEveryReferenceSupplied_Succeeds()
    {
        var result = TraitRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        var result = TraitRating.Create(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("trait_rating.id_required");
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Create_WithAnyReferenceEmpty_Fails(bool emptyResultSet, bool emptyPupil, bool emptyTrait, bool emptyPoint)
    {
        var result = TraitRating.Create(
            Guid.CreateVersion7(),
            emptyResultSet ? Guid.Empty : Guid.CreateVersion7(),
            emptyPupil ? Guid.Empty : Guid.CreateVersion7(),
            emptyTrait ? Guid.Empty : Guid.CreateVersion7(),
            emptyPoint ? Guid.Empty : Guid.CreateVersion7());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("trait_rating.reference_required");
    }

    [Fact]
    public void UpdatePoint_ReplacesOnlyTheChosenPoint()
    {
        var rating = TraitRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        var pupilId = rating.PupilId;
        var traitId = rating.TraitId;
        var newPointId = Guid.CreateVersion7();

        rating.UpdatePoint(newPointId);

        rating.RatingScalePointId.ShouldBe(newPointId);
        rating.PupilId.ShouldBe(pupilId);
        rating.TraitId.ShouldBe(traitId);
    }
}
