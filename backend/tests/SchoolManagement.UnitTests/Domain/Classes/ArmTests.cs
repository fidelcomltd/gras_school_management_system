using SchoolManagement.Domain.Classes;

namespace SchoolManagement.UnitTests.Domain.Classes;

/// <summary>Entity-local invariants for <see cref="Arm"/> (spec 6.4.3, 6.4.6, 6.4.7, 6.4.8).</summary>
public sealed class ArmTests
{
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();

    [Fact]
    public void Create_Defaults_CapacityThirtyAndActiveStatus()
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Capacity.ShouldBe(Arm.DefaultCapacity);
        result.Value.Status.ShouldBe(ArmStatus.Active);
    }

    // Spec 6.4.8: "Label entered as lowercase b: normalised to uppercase on save when the label is a
    // single letter."
    [Theory]
    [InlineData("a", "A")]
    [InlineData("z", "Z")]
    [InlineData("9", "9")]
    public void Create_WithASingleCharacterLabel_NormalisesToUppercase(string typed, string expected)
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, typed, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Label.ShouldBe(expected);
        result.Value.LabelKey.ShouldBe(expected.ToLowerInvariant());
    }

    // Spec 6.4.8: "Multi-character labels keep the case as typed" — "Gold" stays "Gold".
    [Fact]
    public void Create_WithAMultiCharacterLabel_KeepsCaseAsTyped()
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "Gold", null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Label.ShouldBe("Gold");
    }

    [Fact]
    public void Create_TrimsTheLabel()
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "  Gold  ", null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Label.ShouldBe("Gold");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithAnEmptyLabel_Fails(string label)
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, label, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.label_invalid_length");
    }

    [Fact]
    public void Create_WithALabelOverMaxLength_Fails()
    {
        var tooLong = new string('A', Arm.LabelMaxLength + 1);

        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, tooLong, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.label_invalid_length");
    }

    // Spec 6.4.3: "Letters, digits and single internal spaces." Leading/trailing whitespace is
    // TRIMMED first (spec's own word), so " A"/"A " are valid, not rejected — covered separately by
    // Create_TrimsTheLabel; only INTERNAL doubled spaces and other punctuation are rejected here.
    [Theory]
    [InlineData("A-1")]
    [InlineData("A  B")]
    [InlineData("A/B")]
    public void Create_WithDisallowedCharactersOrSpacing_Fails(string label)
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, label, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.label_invalid_characters");
    }

    [Fact]
    public void Create_WithALabelHavingAnInternalSpace_Succeeds()
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "Blue Room", null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Label.ShouldBe("Blue Room");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Create_WithCapacityOutOfRange_Fails(int capacity)
    {
        var result = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", capacity, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.capacity_out_of_range");
    }

    [Fact]
    public void ChangeCapacity_BelowNoOccupancyCheck_Succeeds()
    {
        // Spec 6.4.6: capacity is a SOFT limit — reducing it is always allowed at the entity level;
        // there is no occupancy to check against here because enrolment (Phase 2) does not exist yet.
        var arm = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", 30, null).Value;

        var result = arm.ChangeCapacity(5);

        result.IsSuccess.ShouldBeTrue();
        arm.Capacity.ShouldBe(5);
    }

    [Fact]
    public void AssignFormTeacher_SetsAndClears()
    {
        var arm = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", null, null).Value;
        var teacherId = Guid.CreateVersion7();

        arm.AssignFormTeacher(teacherId);
        arm.FormTeacherAdminId.ShouldBe(teacherId);

        arm.AssignFormTeacher(null);
        arm.FormTeacherAdminId.ShouldBeNull();
    }

    [Fact]
    public void Deactivate_ThenActivate_RoundTrips()
    {
        var arm = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", null, null).Value;

        arm.Deactivate();
        arm.Status.ShouldBe(ArmStatus.Inactive);

        arm.Activate();
        arm.Status.ShouldBe(ArmStatus.Active);
    }

    // Spec 6.4.7: "The arm becomes read-only... no form teacher change" once closed.
    [Fact]
    public void EnsureMutable_OnAClosedArm_FailsNamingTheDisplayName()
    {
        var arm = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", null, null).Value;
        arm.Close();

        var result = arm.EnsureMutable("Primary 1A");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.closed_immutable");
        result.Error.Description.ShouldContain("Primary 1A");
    }

    [Fact]
    public void EnsureMutable_OnAnActiveOrInactiveArm_Succeeds()
    {
        var arm = Arm.Create(Guid.CreateVersion7(), ClassLevelId, SessionId, "A", null, null).Value;

        arm.EnsureMutable("Primary 1A").IsSuccess.ShouldBeTrue();

        arm.Deactivate();
        arm.EnsureMutable("Primary 1A").IsSuccess.ShouldBeTrue();
    }
}
