using SchoolManagement.Domain.Classes;

namespace SchoolManagement.UnitTests.Domain.Classes;

/// <summary>Entity-local invariants for <see cref="ClassLevel"/> (spec 6.4.2). Cross-entity chain rules are <c>ProgressionChainGuardTests</c>.</summary>
public sealed class ClassLevelTests
{
    private static readonly Guid SectionId = Guid.CreateVersion7();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void Create_WithTooShortName_Fails(string name)
    {
        var result = ClassLevel.Create(Guid.CreateVersion7(), name, SectionId, 1, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.name_invalid_length");
    }

    [Fact]
    public void Create_WithNameOverMaxLength_Fails()
    {
        var tooLong = new string('P', ClassLevel.NameMaxLength + 1);

        var result = ClassLevel.Create(Guid.CreateVersion7(), tooLong, SectionId, 1, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.name_invalid_length");
    }

    [Fact]
    public void Create_TrimsName()
    {
        var result = ClassLevel.Create(Guid.CreateVersion7(), "  Primary 1  ", SectionId, 1, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Primary 1");
        result.Value.NameKey.ShouldBe("primary 1");
    }

    [Fact]
    public void Create_WithProgressionOrderBelowOne_Fails()
    {
        var result = ClassLevel.Create(Guid.CreateVersion7(), "Primary 1", SectionId, 0, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.progression_order_invalid");
    }

    [Fact]
    public void Create_PointingAtItself_Fails()
    {
        var id = Guid.CreateVersion7();

        var result = ClassLevel.Create(id, "Primary 4", SectionId, 1, id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_self_reference");
    }

    [Fact]
    public void Create_Defaults_ToActiveStatus()
    {
        var result = ClassLevel.Create(Guid.CreateVersion7(), "Primary 1", SectionId, 1, null);

        result.Value.Status.ShouldBe(LevelStatus.Active);
    }

    [Fact]
    public void SetNextLevel_PointingAtItself_FailsAndLeavesNextLevelUnchanged()
    {
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Primary 1", SectionId, 1, null).Value;
        var otherId = Guid.CreateVersion7();
        level.SetNextLevel(otherId);

        var result = level.SetNextLevel(level.Id);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_self_reference");
        level.NextLevelId.ShouldBe(otherId);
    }

    [Fact]
    public void SetNextLevel_ToNull_ClearsIt()
    {
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Primary 6", SectionId, 1, Guid.CreateVersion7()).Value;

        var result = level.SetNextLevel(null);

        result.IsSuccess.ShouldBeTrue();
        level.NextLevelId.ShouldBeNull();
    }

    [Fact]
    public void ChangeSection_UpdatesSectionId()
    {
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Primary 1", SectionId, 1, null).Value;
        var newSectionId = Guid.CreateVersion7();

        level.ChangeSection(newSectionId);

        level.SectionId.ShouldBe(newSectionId);
    }

    [Fact]
    public void SetProgressionOrder_UpdatesOrder()
    {
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Primary 1", SectionId, 1, null).Value;

        level.SetProgressionOrder(7);

        level.ProgressionOrder.ShouldBe(7);
    }

    [Fact]
    public void Deactivate_ThenActivate_RoundTrips()
    {
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Nursery 1", SectionId, 1, null).Value;

        level.Deactivate();
        level.Status.ShouldBe(LevelStatus.Inactive);

        level.Activate();
        level.Status.ShouldBe(LevelStatus.Active);
    }

    [Fact]
    public void Rename_WithTooShortName_FailsAndLeavesNameUnchanged()
    {
        var level = ClassLevel.Create(Guid.CreateVersion7(), "Primary 1", SectionId, 1, null).Value;

        var result = level.Rename("A");

        result.IsFailure.ShouldBeTrue();
        level.Name.ShouldBe("Primary 1");
    }
}
