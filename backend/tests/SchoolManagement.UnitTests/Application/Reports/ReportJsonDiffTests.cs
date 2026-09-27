using SchoolManagement.Application.Reports;

namespace SchoolManagement.UnitTests.Application.Reports;

/// <summary>The settings change history's plain-language summary of a configuration change.</summary>
public sealed class ReportJsonDiffTests
{
    [Fact]
    public void AChangedBand_IsNamedByItsLetter_EvenWhenTheBandsAreReordered()
    {
        const string before = """{"grading":{"bands":[{"gradeLetter":"A","lowerBound":70},{"gradeLetter":"C","lowerBound":60}]}}""";
        const string after = """{"grading":{"bands":[{"gradeLetter":"C","lowerBound":58},{"gradeLetter":"A","lowerBound":70}]}}""";

        ReportJsonDiff.Describe(before, after).ShouldBe(["grading › bands › C › lower bound changed from 60 to 58"]);
    }

    [Fact]
    public void AListWithRepeatedNames_IsComparedByPosition_AndVersionCountersAreNotChanges()
    {
        const string before = """{"traitsVersionNumber":3,"traits":[{"name":"Neatness","weight":1},{"name":"Neatness","weight":2}]}""";
        const string after = """{"traitsVersionNumber":4,"traits":[{"name":"Neatness","weight":1},{"name":"Neatness","weight":5}]}""";

        ReportJsonDiff.Describe(before, after).ShouldBe(["traits › 2 › weight changed from 2 to 5"]);
    }

    [Fact]
    public void AddedAndRemovedSettings_AreSaid_TheFirstVersionIsNamed_AndALongListIsCut()
    {
        ReportJsonDiff.Describe("""{"a":1}""", """{"b":true}""").ShouldBe(["a removed", "b added"]);
        ReportJsonDiff.Describe(null, "{}").ShouldBe(["First saved configuration."]);
        ReportJsonDiff.Describe("""{"a":1}""", """{"a":1}""").ShouldBe(["No setting changed (saved as it was)."]);

        var many = ReportJsonDiff.Describe("""{"x":[1,2,3,4,5,6,7,8,9,10]}""", """{"x":[2,3,4,5,6,7,8,9,10,11]}""", max: 3);
        many.Count.ShouldBe(4);
        many[^1].ShouldBe("and 7 more changes");
    }
}
