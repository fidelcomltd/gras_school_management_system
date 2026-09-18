using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>
/// Pins spec 6.2.13 / Appendix E.3's four seeded nursery domains and their indicators. Per-domain
/// counts are 4/14/15/12 (45 total) — see <see cref="DevelopmentDomainSeed"/>'s remarks for why this
/// follows the appendix's LISTED items rather than its parenthetical counts ("13", "16"), which do
/// not match what is actually listed for domains 2 and 3.
/// </summary>
public sealed class DevelopmentDomainSeedTests
{
    [Fact]
    public void FourDomains_AreNamedInAppendixEThreePrintedOrder()
    {
        DevelopmentDomainSeed.NurseryDomains.Count.ShouldBe(4);
        DevelopmentDomainSeed.NurseryDomains.Select(domain => domain.Name).ShouldBe(
        [
            "Maths Readiness",
            "Language/Communication Development",
            "Personal & Physical Development",
            "Social & Emotional",
        ]);
        DevelopmentDomainSeed.NurseryDomains.Select(domain => domain.DisplayOrder).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void FortyFiveIndicators_SplitFourFourteenFifteenTwelveAcrossTheFourDomains()
    {
        DevelopmentDomainSeed.NurseryDomains.Select(domain => domain.Indicators.Count).ShouldBe([4, 14, 15, 12]);
        DevelopmentDomainSeed.NurseryDomains.Sum(domain => domain.Indicators.Count).ShouldBe(45);
    }

    [Fact]
    public void EveryDomain_AllowsAnIndicatorComment()
    {
        // E.3: per-indicator Comments column is unique to the nursery sheet's development-domain
        // blocks — all four seeded domains carry it.
        DevelopmentDomainSeed.NurseryDomains.ShouldAllBe(domain => domain.AllowsIndicatorComment);
    }

    [Fact]
    public void EveryDomain_HasIndicatorsOrderedOneUpwardsWithNoDuplicateName()
    {
        foreach (var domain in DevelopmentDomainSeed.NurseryDomains)
        {
            domain.Indicators.Select(indicator => indicator.DisplayOrder)
                .ShouldBe(Enumerable.Range(1, domain.Indicators.Count));

            domain.Indicators.Select(indicator => indicator.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                .Count().ShouldBe(domain.Indicators.Count);
        }
    }

    [Fact]
    public void PersonalAndPhysicalDevelopment_IncludesHomeWorkOnHighQuality()
    {
        // E.3: "including `Home work on High Quality` in domain 3, which had looked like a possible
        // scanning artefact" — confirmed correct by the school. Pinned so a future cleanup cannot
        // quietly drop it as a typo.
        var domain3 = DevelopmentDomainSeed.NurseryDomains.Single(domain => domain.Name == "Personal & Physical Development");

        domain3.Indicators.ShouldContain(indicator => indicator.Name == "Home work on High Quality");
    }
}
