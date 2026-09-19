using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="Trait"/> and <see cref="TraitBlock"/>'s construction and in-place mutators.</summary>
public sealed class TraitTests
{
    [Fact]
    public void Create_TrimsNameAndPreservesEveryOtherField()
    {
        var id = Guid.CreateVersion7();

        var trait = Trait.Create(id, TraitDomain.Affective, "  Punctuality  ", 2, TraitStatus.Active);

        trait.Id.ShouldBe(id);
        trait.Domain.ShouldBe(TraitDomain.Affective);
        trait.Name.ShouldBe("Punctuality");
        trait.DisplayOrder.ShouldBe(2);
        trait.Status.ShouldBe(TraitStatus.Active);
    }

    [Fact]
    public void Update_ChangesEveryFieldExceptId()
    {
        var id = Guid.CreateVersion7();
        var trait = Trait.Create(id, TraitDomain.Affective, "Original", 1, TraitStatus.Active);

        trait.Update(TraitDomain.Psychomotor, "  Renamed  ", 9, TraitStatus.Archived);

        trait.Id.ShouldBe(id);
        trait.Domain.ShouldBe(TraitDomain.Psychomotor);
        trait.Name.ShouldBe("Renamed");
        trait.DisplayOrder.ShouldBe(9);
        trait.Status.ShouldBe(TraitStatus.Archived);
    }

    [Fact]
    public void Block_Create_UsesTheDomainAsItsId()
    {
        var scaleId = Guid.CreateVersion7();

        var block = TraitBlock.Create(TraitDomain.Psychomotor, scaleId);

        block.Id.ShouldBe(TraitDomain.Psychomotor);
        block.RatingScaleId.ShouldBe(scaleId);
    }

    [Fact]
    public void Block_UpdateScale_ChangesTheScaleButNeverTheId()
    {
        var block = TraitBlock.Create(TraitDomain.Affective, Guid.CreateVersion7());
        var newScaleId = Guid.CreateVersion7();

        block.UpdateScale(newScaleId);

        block.Id.ShouldBe(TraitDomain.Affective);
        block.RatingScaleId.ShouldBe(newScaleId);
    }
}
