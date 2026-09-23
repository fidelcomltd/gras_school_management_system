using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Pupils.Import;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.UnitTests.Application.Pupils;

/// <summary>Spec 6.5.13: forgiving about formatting, unforgiving about ambiguity.</summary>
public sealed class PupilImportCellsTests
{
    [Theory]
    [InlineData("03/05/2018")]
    [InlineData("3/5/2018")]
    [InlineData("03-05-2018")]
    [InlineData("03.05.2018")]
    [InlineData("2018-05-03")]
    [InlineData("2018/5/3")]
    public void TryDate_AcceptsDayFirstAndIsoText(string text)
    {
        PupilImportCells.TryDate(Text(text), out var date, out var error).ShouldBeTrue();
        error.ShouldBeNull();
        date.ShouldBe(new DateOnly(2018, 5, 3));
    }

    // 43223 is 3 May 2018 in Excel's 1900 date system.
    [Fact]
    public void TryDate_AcceptsAnExcelSerialTypedAsANumber()
    {
        PupilImportCells.TryDate(new ImportCell("43223", 43223, null), out var date, out _).ShouldBeTrue();
        date.ShouldBe(new DateOnly(2018, 5, 3));
    }

    [Fact]
    public void TryDate_AcceptsADateFormattedCell()
    {
        PupilImportCells.TryDate(new ImportCell("2018-05-03", null, new DateTime(2018, 5, 3, 0, 0, 0, DateTimeKind.Unspecified)), out var date, out _)
            .ShouldBeTrue();
        date.ShouldBe(new DateOnly(2018, 5, 3));
    }

    [Theory]
    [InlineData("03/05/18")]
    [InlineData("3/5/018")]
    public void TryDate_RefusesAShortYearRatherThanGuessing(string text)
    {
        PupilImportCells.TryDate(Text(text), out var date, out var error).ShouldBeFalse();
        date.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("year in full");
    }

    [Theory]
    [InlineData("31/02/2018")]
    [InlineData("13/13/2018")]
    [InlineData("May 3rd")]
    [InlineData("00/00/0000")]
    public void TryDate_RefusesTextThatIsNotARealDate(string text) =>
        PupilImportCells.TryDate(Text(text), out _, out _).ShouldBeFalse();

    [Fact]
    public void TryDate_BlankIsNoDateAndNoError()
    {
        PupilImportCells.TryDate(ImportCell.Blank, out var date, out var error).ShouldBeTrue();
        date.ShouldBeNull();
        error.ShouldBeNull();
    }

    // Spec 6.5.13: a blank health answer stays unanswered, never No.
    [Theory]
    [InlineData("", null)]
    [InlineData("Yes", true)]
    [InlineData("y", true)]
    [InlineData("NO", false)]
    [InlineData("false", false)]
    public void TryYesNo_ReadsAnswersAndKeepsBlankUnanswered(string text, bool? expected)
    {
        PupilImportCells.TryYesNo(Text(text), out var answer, out _).ShouldBeTrue();
        answer.ShouldBe(expected);
    }

    [Fact]
    public void TryYesNo_RefusesAnythingElse() =>
        PupilImportCells.TryYesNo(Text("maybe"), out _, out _).ShouldBeFalse();

    [Theory]
    [InlineData("F", PupilSex.Female)]
    [InlineData("male", PupilSex.Male)]
    public void TrySex_AcceptsLettersAndWords(string text, PupilSex expected)
    {
        PupilImportCells.TrySex(Text(text), out var sex, out _).ShouldBeTrue();
        sex.ShouldBe(expected);
    }

    [Theory]
    [InlineData("AB +", BloodGroup.AbPositive)]
    [InlineData("o-", BloodGroup.ONegative)]
    public void TryBloodGroup_IgnoresCaseAndSpaces(string text, BloodGroup expected)
    {
        PupilImportCells.TryBloodGroup(Text(text), out var group, out _).ShouldBeTrue();
        group.ShouldBe(expected);
    }

    [Fact]
    public void TryGenotype_RefusesAnUnknownValue()
    {
        PupilImportCells.TryGenotype(Text("AX"), out var genotype, out var error).ShouldBeFalse();
        genotype.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("0803 123 4567", "08031234567")]
    [InlineData("0803-123-4567", "08031234567")]
    [InlineData("2348031234567", "+2348031234567")]
    [InlineData("+234 803 123 4567", "+2348031234567")]
    public void Phone_DropsSeparators(string text, string expected) =>
        PupilImportCells.Phone(Text(text)).ShouldBe(expected);

    // A phone typed into a General cell is stored as the number 8031234567: the leading zero is the spreadsheet's loss.
    [Fact]
    public void Phone_RestoresTheLeadingZeroANumberCellDropped() =>
        PupilImportCells.Phone(new ImportCell("8031234567", 8031234567, null)).ShouldBe("08031234567");

    [Fact]
    public void Phone_LeavesTenDigitTextAloneForThePhoneRuleToRefuse() =>
        PupilImportCells.Phone(Text("8031234567")).ShouldBe("8031234567");

    [Theory]
    [InlineData("Date of Birth", "dateofbirth")]
    [InlineData("date_of_birth", "dateofbirth")]
    [InlineData(" DateOfBirth ", "dateofbirth")]
    public void Key_MatchesHeadersWhateverTheirSpacingAndCase(string header, string expected) =>
        PupilImportColumns.Key(header).ShouldBe(expected);

    private static ImportCell Text(string text) => new(text, null, null);
}
