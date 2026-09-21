using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>Entity-local invariants for <see cref="RemarkTemplate"/> (TASK-0086 stage B).</summary>
public sealed class RemarkTemplateTests
{
    [Fact]
    public void Create_WithValidText_TrimsAndLowersTheComparisonKey()
    {
        var result = RemarkTemplate.Create(Guid.CreateVersion7(), RemarkKind.ClassTeacher, "  A Pleasure To Have In School.  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Text.ShouldBe("A Pleasure To Have In School.");
        result.Value.TextKey.ShouldBe("a pleasure to have in school.");
        result.Value.Kind.ShouldBe(RemarkKind.ClassTeacher);
    }

    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        var result = RemarkTemplate.Create(Guid.Empty, RemarkKind.HeadTeacher, "Text");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.id_required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankText_Fails(string text)
    {
        var result = RemarkTemplate.Create(Guid.CreateVersion7(), RemarkKind.ClassTeacher, text);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.text_invalid");
    }

    [Fact]
    public void Create_WithTextOverMaxLength_Fails()
    {
        var result = RemarkTemplate.Create(Guid.CreateVersion7(), RemarkKind.ClassTeacher, new string('a', RemarkTemplate.TextMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.text_invalid");
    }

    [Fact]
    public void Create_WithTextAtExactlyMaxLength_Succeeds()
    {
        var result = RemarkTemplate.Create(Guid.CreateVersion7(), RemarkKind.ClassTeacher, new string('a', RemarkTemplate.TextMaxLength));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithASingleCharacter_Succeeds()
    {
        var result = RemarkTemplate.Create(Guid.CreateVersion7(), RemarkKind.HeadTeacher, "A");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Text.ShouldBe("A");
    }
}
