using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>Entity-local invariants for <see cref="DevelopmentRating"/> (TASK-0083 stage 2).</summary>
public sealed class DevelopmentRatingTests
{
    [Fact]
    public void Create_WithEveryReferenceSupplied_Succeeds()
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: "Needs reminding after lunch.");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Comment.ShouldBe("Needs reminding after lunch.");
    }

    [Fact]
    public void Create_WithNoComment_Succeeds()
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Comment.ShouldBeNull();
    }

    [Fact]
    public void Create_WithAWhitespaceOnlyComment_NormalizesToNull()
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: "   ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Comment.ShouldBeNull();
    }

    [Fact]
    public void Create_TrimsTheComment()
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: "  Needs reminding.  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Comment.ShouldBe("Needs reminding.");
    }

    [Fact]
    public void Create_WithACommentOver120Characters_Fails()
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: new string('a', DevelopmentRating.CommentMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("development_rating.comment_too_long");
    }

    [Fact]
    public void Create_WithACommentExactly120Characters_Succeeds()
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: new string('a', DevelopmentRating.CommentMaxLength));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        var result = DevelopmentRating.Create(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), comment: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("development_rating.id_required");
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Create_WithAnyReferenceEmpty_Fails(bool emptyResultSet, bool emptyPupil, bool emptyIndicator, bool emptyPoint)
    {
        var result = DevelopmentRating.Create(
            Guid.CreateVersion7(),
            emptyResultSet ? Guid.Empty : Guid.CreateVersion7(),
            emptyPupil ? Guid.Empty : Guid.CreateVersion7(),
            emptyIndicator ? Guid.Empty : Guid.CreateVersion7(),
            emptyPoint ? Guid.Empty : Guid.CreateVersion7(),
            comment: null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("development_rating.reference_required");
    }

    [Fact]
    public void UpdateRating_ReplacesBothThePointAndTheComment()
    {
        var rating = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: "Original comment.").Value;
        var pupilId = rating.PupilId;
        var indicatorId = rating.IndicatorId;
        var newPointId = Guid.CreateVersion7();

        rating.UpdateRating(newPointId, "Updated comment.");

        rating.RatingScalePointId.ShouldBe(newPointId);
        rating.Comment.ShouldBe("Updated comment.");
        rating.PupilId.ShouldBe(pupilId);
        rating.IndicatorId.ShouldBe(indicatorId);
    }

    [Fact]
    public void UpdateRating_WithANullComment_ClearsTheExistingComment()
    {
        var rating = DevelopmentRating.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            comment: "Original comment.").Value;

        rating.UpdateRating(Guid.CreateVersion7(), null);

        rating.Comment.ShouldBeNull();
    }
}
