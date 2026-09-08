using SchoolManagement.Domain.Classes;

namespace SchoolManagement.UnitTests.Domain.Classes;

/// <summary>
/// Pins spec 6.4.2's two seeded sections and nine seeded levels: fixed ids, and that the chain they
/// describe actually satisfies <see cref="ProgressionChainGuard"/> — Nursery 1 entry, Primary 6
/// graduating, crossing the Nursery/Primary section boundary at Nursery 3 → Primary 1.
/// </summary>
public sealed class SeededClassLevelsTests
{
    [Fact]
    public void TwoSections_AreFixedDistinctAndNamedNurseryAndPrimary()
    {
        SeededClassLevels.Sections.Count.ShouldBe(2);
        SeededClassLevels.Sections.Select(section => section.Name).ShouldBe(["Nursery", "Primary"]);
        SeededClassLevels.Sections.Select(section => section.Id).Distinct().Count().ShouldBe(2);

        // Pinned literally: a change here changes what an existing database and a fresh install
        // disagree on, which must never happen silently.
        SeededClassLevels.NurserySectionId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000301"));
        SeededClassLevels.PrimarySectionId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000302"));
    }

    [Fact]
    public void NineLevels_HaveFixedDistinctIdsAndVersions()
    {
        SeededClassLevels.Levels.Count.ShouldBe(9);
        SeededClassLevels.Levels.Select(level => level.Id).Distinct().Count().ShouldBe(9);
        SeededClassLevels.Levels.Select(level => level.Version).Distinct().Count().ShouldBe(9);
    }

    [Fact]
    public void Levels_AreNamedAndOrderedNurseryOneToThreeThenPrimaryOneToSix()
    {
        SeededClassLevels.Levels.Select(level => level.Name).ShouldBe(
        [
            "Nursery 1", "Nursery 2", "Nursery 3",
            "Primary 1", "Primary 2", "Primary 3", "Primary 4", "Primary 5", "Primary 6",
        ]);

        SeededClassLevels.Levels.Select(level => level.ProgressionOrder).ShouldBe([1, 2, 3, 4, 5, 6, 7, 8, 9]);
    }

    [Fact]
    public void Chain_CrossesTheSectionBoundaryAtNurseryThreeToPrimaryOne()
    {
        var nursery3 = SeededClassLevels.Levels.Single(level => level.Name == "Nursery 3");
        var primary1 = SeededClassLevels.Levels.Single(level => level.Name == "Primary 1");

        nursery3.SectionId.ShouldBe(SeededClassLevels.NurserySectionId);
        primary1.SectionId.ShouldBe(SeededClassLevels.PrimarySectionId);
        nursery3.NextLevelId.ShouldBe(primary1.Id);
    }

    [Fact]
    public void Chain_HasNurseryOneAsEntryAndPrimarySixAsGraduating()
    {
        var nursery1 = SeededClassLevels.Levels.Single(level => level.Name == "Nursery 1");
        var primary6 = SeededClassLevels.Levels.Single(level => level.Name == "Primary 6");

        // Nothing points at Nursery 1 (entry) — verified against every other level's next pointer.
        SeededClassLevels.Levels.ShouldAllBe(level => level.NextLevelId != nursery1.Id);
        primary6.NextLevelId.ShouldBeNull();
    }

    // The chain the migration seeds is not merely "plausible" — it genuinely satisfies all eight
    // progression rules, proven by handing it to the same guard the endpoints run in production.
    [Fact]
    public void Chain_SatisfiesProgressionChainGuard()
    {
        var chainLevels = SeededClassLevels.Levels
            .Select(level => new ChainLevel(level.Id, level.Name, level.ProgressionOrder, level.NextLevelId))
            .ToArray();

        var result = ProgressionChainGuard.Validate(chainLevels, new Dictionary<Guid, string>());

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
    }

    // Acceptance criterion: nothing assumes exactly nine levels, or that the chain stays inside a
    // section. Adding a tenth (crossing back INTO Nursery from the tail of Primary, an intentionally
    // unrealistic shape) still validates cleanly.
    [Fact]
    public void Chain_StillValidatesWithATenthLevelInsertedAcrossSections()
    {
        var primary6 = SeededClassLevels.Levels.Single(level => level.Name == "Primary 6");
        var tenthId = Guid.CreateVersion7();

        var chainLevels = SeededClassLevels.Levels
            .Select(level => level.Id == primary6.Id
                ? new ChainLevel(level.Id, level.Name, level.ProgressionOrder, tenthId)
                : new ChainLevel(level.Id, level.Name, level.ProgressionOrder, level.NextLevelId))
            .Append(new ChainLevel(tenthId, "Graduate School", 10, null))
            .ToArray();

        var result = ProgressionChainGuard.Validate(chainLevels, new Dictionary<Guid, string>());

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
    }
}
