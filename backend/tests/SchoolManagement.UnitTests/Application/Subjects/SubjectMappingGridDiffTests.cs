using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="SubjectMappingGridDiff"/> — the single diff computation shared by the grid save,
/// copy and prefill handlers. Pure function, no fakes needed.
/// </summary>
public sealed class SubjectMappingGridDiffTests
{
    private static SubjectMapping Mapping(Guid subjectId, Guid levelId, int displayOrder = 1) =>
        SubjectMapping.Create(Guid.CreateVersion7(), subjectId, levelId, Guid.CreateVersion7(), Guid.CreateVersion7(), displayOrder).Value;

    // Spec 6.6.8's own worked example: "Copy from a term with 14 mappings into a term that already
    // has 3. The preview shows 11 additions and 0 endings, and does not duplicate the 3."
    [Fact]
    public void Compute_AdditiveOnly_NeverProducesEndings_AndDoesNotDuplicateExistingPairs()
    {
        var levelId = Guid.CreateVersion7();
        var existingSubjectIds = Enumerable.Range(0, 3).Select(_ => Guid.CreateVersion7()).ToArray();
        var newSubjectIds = Enumerable.Range(0, 11).Select(_ => Guid.CreateVersion7()).ToArray();

        var current = existingSubjectIds.Select(id => Mapping(id, levelId)).ToArray();
        var desired = existingSubjectIds.Concat(newSubjectIds)
            .Select(id => new DesiredSubjectMappingEntry(id, levelId, 1))
            .ToArray();

        var diff = SubjectMappingGridDiff.Compute(current, desired, additiveOnly: true);

        diff.Additions.Count.ShouldBe(11);
        diff.Endings.ShouldBeEmpty();
    }

    [Fact]
    public void Compute_NonAdditive_TreatsACurrentPairAbsentFromDesired_AsAnEnding()
    {
        var levelId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();
        var current = new[] { Mapping(subjectId, levelId) };

        var diff = SubjectMappingGridDiff.Compute(current, desired: [], additiveOnly: false);

        diff.Additions.ShouldBeEmpty();
        diff.Endings.ShouldBe(current);
    }

    [Fact]
    public void Compute_WhenTheSamePairAppearsInBoth_WithADifferentDisplayOrder_ProducesAReorderNotAnAdditionOrEnding()
    {
        var levelId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();
        var current = new[] { Mapping(subjectId, levelId, displayOrder: 1) };
        var desired = new[] { new DesiredSubjectMappingEntry(subjectId, levelId, 5) };

        var diff = SubjectMappingGridDiff.Compute(current, desired, additiveOnly: false);

        diff.Additions.ShouldBeEmpty();
        diff.Endings.ShouldBeEmpty();
        diff.Reorders.Count.ShouldBe(1);
        diff.Reorders[0].Mapping.ShouldBe(current[0]);
        diff.Reorders[0].NewDisplayOrder.ShouldBe(5);
    }

    [Fact]
    public void Compute_WhenTheSamePairAppearsInBoth_WithTheSameDisplayOrder_ProducesNoChangeAtAll()
    {
        var levelId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();
        var current = new[] { Mapping(subjectId, levelId, displayOrder: 3) };
        var desired = new[] { new DesiredSubjectMappingEntry(subjectId, levelId, 3) };

        var diff = SubjectMappingGridDiff.Compute(current, desired, additiveOnly: false);

        diff.Additions.ShouldBeEmpty();
        diff.Endings.ShouldBeEmpty();
        diff.Reorders.ShouldBeEmpty();
    }
}
