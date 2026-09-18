using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Domain.Subjects;

/// <summary>
/// Tests <see cref="Subject"/> — spec 6.6.2's field rules, and TASK-0070 delta amendment 1's
/// departure making <c>code</c> nullable and optional.
/// </summary>
public sealed class SubjectTests
{
    [Fact]
    public void Create_WithNoCode_Succeeds_AndCodeStaysNull()
    {
        var result = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBeNull();
        result.Value.CodeKey.ShouldBeNull();
        result.Value.Status.ShouldBe(SubjectStatus.Active);
    }

    [Fact]
    public void Create_WithAValidCode_Succeeds()
    {
        var result = Subject.Create(Guid.CreateVersion7(), "Mathematics", "MTH", null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("MTH");
        result.Value.CodeKey.ShouldBe("mth");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithAnEmptyName_IsRejected(string name)
    {
        var result = Subject.Create(Guid.CreateVersion7(), name, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.name_invalid_length");
    }

    [Fact]
    public void Create_WithANameLongerThanEightyCharacters_IsRejected()
    {
        var result = Subject.Create(Guid.CreateVersion7(), new string('A', Subject.NameMaxLength + 1), null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.name_invalid_length");
    }

    [Theory]
    [InlineData("mth")] // lowercase
    [InlineData("MT-H")] // punctuation
    [InlineData("MTH ENGLISH")] // space
    public void Create_WithAnInvalidCode_IsRejected(string code)
    {
        var result = Subject.Create(Guid.CreateVersion7(), "Mathematics", code, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.code_invalid_characters");
    }

    [Fact]
    public void Create_WithACodeLongerThanTwelveCharacters_IsRejected()
    {
        var result = Subject.Create(Guid.CreateVersion7(), "Mathematics", new string('A', Subject.CodeMaxLength + 1), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.code_invalid_length");
    }

    [Fact]
    public void Create_WithADescriptionLongerThanThreeHundredCharacters_IsRejected()
    {
        var result = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, new string('A', Subject.DescriptionMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.description_invalid_length");
    }

    [Fact]
    public void Create_WithAnEmptyId_IsRejected()
    {
        var result = Subject.Create(Guid.Empty, "Mathematics", null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.id_required");
    }

    [Fact]
    public void Edit_WithAnEmptyStringCode_ClearsIt()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", "MTH", null).Value;

        var result = subject.Edit("Mathematics", string.Empty, null);

        result.IsSuccess.ShouldBeTrue();
        subject.Code.ShouldBeNull();
        subject.CodeKey.ShouldBeNull();
    }

    [Fact]
    public void Deactivate_ThenActivate_RoundTripsTheStatus()
    {
        var subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;

        subject.Deactivate();
        subject.Status.ShouldBe(SubjectStatus.Inactive);

        subject.Activate();
        subject.Status.ShouldBe(SubjectStatus.Active);
    }
}
