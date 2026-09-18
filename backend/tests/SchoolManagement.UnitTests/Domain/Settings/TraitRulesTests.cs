using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="TraitRules"/> against spec 6.2.7's structural save-time rule.</summary>
public sealed class TraitRulesTests
{
    [Fact]
    public void ValidateWholeSet_AcceptsAWellFormedSet()
    {
        var result = TraitRules.ValidateWholeSet(
        [
            new TraitInput(TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active),
            new TraitInput(TraitDomain.Psychomotor, "Sports", 1, TraitStatus.Active),
        ]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeSet_AcceptsAnEmptySet()
    {
        // No minimum-trait-count rule — spec 6.2.7 never states one, matching
        // DevelopmentDomainRules' own "an empty submission is structurally valid" reasoning.
        var result = TraitRules.ValidateWholeSet([]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeSet_RejectsADuplicateNameWithinTheSameDomainCaseInsensitively()
    {
        var result = TraitRules.ValidateWholeSet(
        [
            new TraitInput(TraitDomain.Affective, "Punctuality", 1, TraitStatus.Active),
            new TraitInput(TraitDomain.Affective, "punctuality", 2, TraitStatus.Active),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(TraitRules.DuplicateNameCode);
        var error = result.Error.ShouldBeOfType<TraitValidationError>();
        error.TraitIndex.ShouldBe(1);
    }

    [Fact]
    public void ValidateWholeSet_AcceptsTheSameNameInTheOtherDomain()
    {
        // Spec 6.2.7: "Unique within its domain" — the same name is allowed in the other domain.
        var result = TraitRules.ValidateWholeSet(
        [
            new TraitInput(TraitDomain.Affective, "Skills", 1, TraitStatus.Active),
            new TraitInput(TraitDomain.Psychomotor, "Skills", 1, TraitStatus.Active),
        ]);

        result.IsSuccess.ShouldBeTrue();
    }
}
