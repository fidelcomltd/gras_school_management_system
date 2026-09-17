using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Domain.Subjects;

/// <summary>Tests <see cref="SubjectMappingException"/> — spec 6.6.4.</summary>
public sealed class SubjectMappingExceptionTests
{
    [Fact]
    public void Create_WithAValidReasonAndReferences_Succeeds()
    {
        var result = SubjectMappingException.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            SubjectExceptionMode.Include, "The level's core curriculum requires it.");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Mode.ShouldBe(SubjectExceptionMode.Include);
        result.Value.Reason.ShouldBe("The level's core curriculum requires it.");
    }

    [Fact]
    public void Create_WithAnEmptyId_IsRejected()
    {
        var result = SubjectMappingException.Create(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            SubjectExceptionMode.Include, "A reason");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.id_required");
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_WithAnyReferenceEmpty_IsRejected(bool emptyArm, bool emptySubject, bool emptyTerm)
    {
        var result = SubjectMappingException.Create(
            Guid.CreateVersion7(),
            emptyArm ? Guid.Empty : Guid.CreateVersion7(),
            emptySubject ? Guid.Empty : Guid.CreateVersion7(),
            emptyTerm ? Guid.Empty : Guid.CreateVersion7(),
            SubjectExceptionMode.Exclude,
            "A reason");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.reference_required");
    }

    // Spec 6.6.4: "an exception without a stated reason becomes a mystery within one term."
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithAnEmptyReason_IsRejected(string reason)
    {
        var result = SubjectMappingException.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            SubjectExceptionMode.Include, reason);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.reason_invalid_length");
    }

    [Fact]
    public void Create_WithAReasonLongerThanTwoHundredCharacters_IsRejected()
    {
        var result = SubjectMappingException.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            SubjectExceptionMode.Include, new string('A', SubjectMappingException.ReasonMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_exception.reason_invalid_length");
    }
}
