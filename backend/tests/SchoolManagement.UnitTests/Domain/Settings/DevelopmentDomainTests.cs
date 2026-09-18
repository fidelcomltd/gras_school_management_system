using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="DevelopmentDomain"/> and <see cref="DevelopmentIndicator"/>'s construction and in-place mutators.</summary>
public sealed class DevelopmentDomainTests
{
    [Fact]
    public void Create_TrimsNameAndPreservesEveryOtherField()
    {
        var id = Guid.CreateVersion7();
        var sectionId = Guid.CreateVersion7();
        var scaleId = Guid.CreateVersion7();

        var domain = DevelopmentDomain.Create(id, sectionId, "  Maths Readiness  ", 1, scaleId, true, DevelopmentDomainStatus.Active, []);

        domain.Id.ShouldBe(id);
        domain.SectionId.ShouldBe(sectionId);
        domain.Name.ShouldBe("Maths Readiness");
        domain.DisplayOrder.ShouldBe(1);
        domain.RatingScaleId.ShouldBe(scaleId);
        domain.AllowsIndicatorComment.ShouldBeTrue();
        domain.Status.ShouldBe(DevelopmentDomainStatus.Active);
    }

    [Fact]
    public void Update_ChangesEveryFieldExceptId()
    {
        var id = Guid.CreateVersion7();
        var domain = DevelopmentDomain.Create(id, Guid.CreateVersion7(), "Original", 1, Guid.CreateVersion7(), true, DevelopmentDomainStatus.Active, []);

        var newSectionId = Guid.CreateVersion7();
        var newScaleId = Guid.CreateVersion7();
        domain.Update(newSectionId, "  Renamed  ", 2, newScaleId, false, DevelopmentDomainStatus.Archived);

        domain.Id.ShouldBe(id);
        domain.SectionId.ShouldBe(newSectionId);
        domain.Name.ShouldBe("Renamed");
        domain.DisplayOrder.ShouldBe(2);
        domain.RatingScaleId.ShouldBe(newScaleId);
        domain.AllowsIndicatorComment.ShouldBeFalse();
        domain.Status.ShouldBe(DevelopmentDomainStatus.Archived);
    }

    [Fact]
    public void Indicator_Create_TrimsNameAndPreservesEveryOtherField()
    {
        var id = Guid.CreateVersion7();
        var domainId = Guid.CreateVersion7();

        var indicator = DevelopmentIndicator.Create(id, domainId, "  Potty trained  ", 5, DevelopmentIndicatorStatus.Active);

        indicator.Id.ShouldBe(id);
        indicator.DomainId.ShouldBe(domainId);
        indicator.Name.ShouldBe("Potty trained");
        indicator.DisplayOrder.ShouldBe(5);
        indicator.Status.ShouldBe(DevelopmentIndicatorStatus.Active);
    }

    [Fact]
    public void Indicator_Update_ChangesEveryFieldExceptIdAndDomainId()
    {
        var id = Guid.CreateVersion7();
        var domainId = Guid.CreateVersion7();
        var indicator = DevelopmentIndicator.Create(id, domainId, "Original", 1, DevelopmentIndicatorStatus.Active);

        indicator.Update("  Renamed  ", 9, DevelopmentIndicatorStatus.Archived);

        indicator.Id.ShouldBe(id);
        indicator.DomainId.ShouldBe(domainId);
        indicator.Name.ShouldBe("Renamed");
        indicator.DisplayOrder.ShouldBe(9);
        indicator.Status.ShouldBe(DevelopmentIndicatorStatus.Archived);
    }
}
