using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="DevelopmentDomainRules"/> against spec 6.2.13's structural save-time rules.</summary>
public sealed class DevelopmentDomainRulesTests
{
    private static readonly Guid NurserySectionId = Guid.CreateVersion7();
    private static readonly Guid PrimarySectionId = Guid.CreateVersion7();
    private static readonly Guid RatingScaleId = Guid.CreateVersion7();

    private static readonly IReadOnlyList<DevelopmentIndicatorInput> OneIndicator =
    [
        new("Potty trained", 1, DevelopmentIndicatorStatus.Active),
    ];

    [Fact]
    public void ValidateWholeSet_AcceptsAWellFormedDomain()
    {
        var result = DevelopmentDomainRules.ValidateWholeSet(
        [
            new DevelopmentDomainInput(NurserySectionId, "Personal & Physical Development", 1, RatingScaleId, true, DevelopmentDomainStatus.Active, OneIndicator),
        ]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeSet_AcceptsAnEmptySet()
    {
        // No minimum-domain-count rule — spec 6.2.13 never states one, matching RatingScaleRules'
        // own "an empty submission is structurally valid" reasoning.
        var result = DevelopmentDomainRules.ValidateWholeSet([]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWholeSet_RejectsADuplicateIndicatorNameWithinADomain()
    {
        var result = DevelopmentDomainRules.ValidateWholeSet(
        [
            new DevelopmentDomainInput(
                NurserySectionId,
                "Personal & Physical Development",
                1,
                RatingScaleId,
                true,
                DevelopmentDomainStatus.Active,
                [
                    new DevelopmentIndicatorInput("Potty trained", 1, DevelopmentIndicatorStatus.Active),
                    new DevelopmentIndicatorInput("potty trained", 2, DevelopmentIndicatorStatus.Active),
                ]),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DevelopmentDomainRules.DuplicateIndicatorNameCode);
        var error = result.Error.ShouldBeOfType<DevelopmentDomainValidationError>();
        error.DomainIndex.ShouldBe(0);
        error.IndicatorIndex.ShouldBe(1);
    }

    [Fact]
    public void ValidateWholeSet_RejectsTwoDomainsWithTheSameNameInTheSameSectionCaseInsensitively()
    {
        var result = DevelopmentDomainRules.ValidateWholeSet(
        [
            new DevelopmentDomainInput(NurserySectionId, "Maths Readiness", 1, RatingScaleId, true, DevelopmentDomainStatus.Active, OneIndicator),
            new DevelopmentDomainInput(NurserySectionId, "maths readiness", 2, RatingScaleId, true, DevelopmentDomainStatus.Active, OneIndicator),
        ]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DevelopmentDomainRules.DuplicateNameCode);
        var error = result.Error.ShouldBeOfType<DevelopmentDomainValidationError>();
        error.DomainIndex.ShouldBe(1);
        error.IndicatorIndex.ShouldBeNull();
    }

    [Fact]
    public void ValidateWholeSet_AcceptsTheSameDomainNameInDifferentSections()
    {
        // Spec 6.2.13: domains are section-scoped, so the same name is allowed in a different section.
        var result = DevelopmentDomainRules.ValidateWholeSet(
        [
            new DevelopmentDomainInput(NurserySectionId, "Development", 1, RatingScaleId, true, DevelopmentDomainStatus.Active, OneIndicator),
            new DevelopmentDomainInput(PrimarySectionId, "Development", 1, RatingScaleId, true, DevelopmentDomainStatus.Active, OneIndicator),
        ]);

        result.IsSuccess.ShouldBeTrue();
    }
}
