using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Domain.Subjects;

/// <summary>Tests <see cref="SubjectMapping"/> — spec 6.6.3.</summary>
public sealed class SubjectMappingTests
{
    [Fact]
    public void Create_WithValidReferences_Succeeds_AndDefaultsActive()
    {
        var result = SubjectMapping.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(SubjectMappingStatus.Active);
    }

    [Fact]
    public void Create_WithAnEmptyId_IsRejected()
    {
        var result = SubjectMapping.Create(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.id_required");
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Create_WithAnyReferenceEmpty_IsRejected(bool emptySubject, bool emptyLevel, bool emptySession, bool emptyTerm)
    {
        var result = SubjectMapping.Create(
            Guid.CreateVersion7(),
            emptySubject ? Guid.Empty : Guid.CreateVersion7(),
            emptyLevel ? Guid.Empty : Guid.CreateVersion7(),
            emptySession ? Guid.Empty : Guid.CreateVersion7(),
            emptyTerm ? Guid.Empty : Guid.CreateVersion7(),
            1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject_mapping.reference_required");
    }

    [Fact]
    public void ChangeDisplayOrder_UpdatesTheStoredValue()
    {
        var mapping = SubjectMapping.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1).Value;

        mapping.ChangeDisplayOrder(7);

        mapping.DisplayOrder.ShouldBe(7);
    }

    [Fact]
    public void End_MovesStatusToEnded()
    {
        var mapping = SubjectMapping.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1).Value;

        mapping.End();

        mapping.Status.ShouldBe(SubjectMappingStatus.Ended);
    }
}
