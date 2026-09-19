using SchoolManagement.Domain.Classes;

namespace SchoolManagement.UnitTests.Domain.Classes;

/// <summary>Entity-local invariants for <see cref="Section"/> (spec 6.4.2, 6.4.9).</summary>
public sealed class SectionTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void Create_WithTooShortName_Fails(string name)
    {
        var result = Section.Create(Guid.CreateVersion7(), name);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("section.name_invalid_length");
    }

    [Fact]
    public void Create_WithNameOverMaxLength_Fails()
    {
        var tooLong = new string('N', Section.NameMaxLength + 1);

        var result = Section.Create(Guid.CreateVersion7(), tooLong);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("section.name_invalid_length");
    }

    [Fact]
    public void Create_TrimsNameAndLowersNameKey()
    {
        var result = Section.Create(Guid.CreateVersion7(), "  Nursery  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Nursery");
        result.Value.NameKey.ShouldBe("nursery");
    }

    [Fact]
    public void Rename_WithValidName_Succeeds()
    {
        var section = Section.Create(Guid.CreateVersion7(), "Nursery").Value;

        var result = section.Rename("Secondary");

        result.IsSuccess.ShouldBeTrue();
        section.Name.ShouldBe("Secondary");
        section.NameKey.ShouldBe("secondary");
    }

    [Fact]
    public void Rename_WithTooLongName_FailsAndLeavesNameUnchanged()
    {
        var section = Section.Create(Guid.CreateVersion7(), "Nursery").Value;

        var result = section.Rename(new string('N', Section.NameMaxLength + 1));

        result.IsFailure.ShouldBeTrue();
        section.Name.ShouldBe("Nursery");
    }

    [Fact]
    public void Create_WithRatesTraitsOmitted_DefaultsFalse()
    {
        var result = Section.Create(Guid.CreateVersion7(), "Secondary");

        result.IsSuccess.ShouldBeTrue();
        result.Value.RatesTraits.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithRatesTraitsTrue_SetsIt()
    {
        var result = Section.Create(Guid.CreateVersion7(), "Secondary", ratesTraits: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RatesTraits.ShouldBeTrue();
    }

    [Fact]
    public void SetRatesTraits_ChangesTheFlagOnly()
    {
        var section = Section.Create(Guid.CreateVersion7(), "Nursery", ratesTraits: false).Value;

        section.SetRatesTraits(true);

        section.RatesTraits.ShouldBeTrue();
        section.Name.ShouldBe("Nursery");
    }
}
