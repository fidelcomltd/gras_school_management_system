using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results.Sheets;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Application.Results.Sheets;

public sealed class ResultSheetBuilderTests
{
    private static readonly Guid Ca1 = Guid.Parse("11111111-0000-7000-8000-000000000001");
    private static readonly Guid Ca2 = Guid.Parse("11111111-0000-7000-8000-000000000002");
    private static readonly Guid Exam = Guid.Parse("11111111-0000-7000-8000-000000000003");
    private static readonly Guid Maths = Guid.Parse("22222222-0000-7000-8000-000000000001");
    private static readonly Guid English = Guid.Parse("22222222-0000-7000-8000-000000000002");
    private static readonly Guid Scale = Guid.Parse("33333333-0000-7000-8000-000000000001");
    private static readonly Guid PointE = Guid.Parse("33333333-0000-7000-8000-000000000002");
    private static readonly Guid PointS = Guid.Parse("33333333-0000-7000-8000-000000000003");
    private static readonly Guid Section = Guid.Parse("44444444-0000-7000-8000-000000000001");
    private static readonly Guid Indicator = Guid.Parse("55555555-0000-7000-8000-000000000001");

    [Fact]
    public void Nursery_RendersDomainsWithComments_NoAttendance_AndBlankForUnenteredMarks()
    {
        var sheet = ResultSheetBuilder.Build(Data(NurserySnapshot, revision: 1))!;

        sheet.Section.ShouldBe(SheetSection.Nursery);
        sheet.Attendance.ShouldBeNull();
        sheet.ClassName.ShouldBe("Nursery 2A");
        sheet.Columns.Select(column => column.Label).ShouldBe(["1st CA", "2nd CA", "Exam"]);

        var maths = sheet.Subjects.Single(row => row.Subject == "Mathematics");
        maths.Cells.ShouldBe(["18", "16", "ABS"]);
        var english = sheet.Subjects.Single(row => row.Subject == "English");
        english.Cells.ShouldBe([null, null, null]);
        english.Total.ShouldBeNull();
        sheet.GrandTotals.ShouldBe([18, 16, null, 34]);

        var block = sheet.RatingBlocks.Single();
        block.Name.ShouldBe("PHYSICAL DEVELOPMENT");
        block.PointCodes.ShouldBe(["E", "S"]);
        block.Items.Single().PointCode.ShouldBe("S");
        block.Items.Single().Comment.ShouldBe("Runs well.");
        sheet.RatingKey.ShouldBe("E-Excellent  S-Satisfied");
        sheet.Age.ShouldBe(4);
        sheet.PupilName.ShouldBe("OKAFOR Chidera");
        sheet.RevisionNotice.ShouldBeNull();
        sheet.GradeKey.Single().ShouldBe(new SheetGradeKeyRow("A", "85-89", "Excellent"));
    }

    [Fact]
    public void ARevisedSheet_CarriesTheRevisionNotice()
    {
        var sheet = ResultSheetBuilder.Build(Data(NurserySnapshot, revision: 2))!;

        sheet.RevisionNotice.ShouldBe("Revised result, issued 19/01/2027. This replaces the version issued 14/12/2026.");
    }

    [Fact]
    public void ASetNeverPublished_HasNoSheet() =>
        ResultSheetBuilder.Build(Data(null, revision: 0)).ShouldBeNull();

    [Theory]
    [InlineData("2022-06-01", "2026-12-15", 4)]
    [InlineData("2022-12-16", "2026-12-15", 3)]
    [InlineData("2022-12-15", "2026-12-15", 4)]
    public void Age_IsWholeYearsAtTheTermEnd(string born, string termEnd, int expected) =>
        ResultSheetBuilder.AgeAt(DateOnly.Parse(born), DateOnly.Parse(termEnd)).ShouldBe(expected);

    private static ResultSheetData Data(string? snapshot, int revision) => new(
        Guid.CreateVersion7(),
        ResultSetState.Published,
        snapshot,
        revision,
        new DateTimeOffset(2027, 1, 19, 9, 0, 0, TimeSpan.Zero),
        revision > 1 ? new DateTimeOffset(2026, 12, 14, 9, 0, 0, TimeSpan.Zero) : null,
        "Okafor",
        "Chidera",
        new DateOnly(2022, 6, 1),
        "GRAS/2026/0041",
        [new SheetScoreRow(Maths, $"{{\"{Ca1}\":18,\"{Ca2}\":16}}", null, ExamAbsent: true, Voided: false)],
        [new SheetLineRow(Maths, 34, "E", "Not Now")],
        34.00m,
        "E",
        [],
        [new SheetRatingRow(Indicator, PointS, "Runs well.")],
        50,
        "Good.",
        "Well done.");

    private static readonly string NurserySnapshot = $$"""
        {
          "settings": {
            "identity": { "schoolName": "Golden Royal Ark School" },
            "assessment": { "components": [
              { "id": "{{Exam}}", "name": "Examination", "shortLabel": "Exam", "maxMark": 60, "isExamination": true, "displayOrder": 3 },
              { "id": "{{Ca1}}", "name": "First CA", "shortLabel": "1st CA", "maxMark": 20, "isExamination": false, "displayOrder": 1 },
              { "id": "{{Ca2}}", "name": "Second CA", "shortLabel": "2nd CA", "maxMark": 20, "isExamination": false, "displayOrder": 2 }
            ] },
            "grading": { "bands": [ { "gradeLetter": "A", "lowerBound": 85, "upperBound": 89, "remark": "Excellent", "displayOrder": 1 } ] },
            "ratingScales": { "scales": [ { "id": "{{Scale}}", "points": [
              { "id": "{{PointS}}", "pointCode": "S", "pointLabel": "Satisfied", "pointOrder": 2 },
              { "id": "{{PointE}}", "pointCode": "E", "pointLabel": "Excellent", "pointOrder": 1 }
            ] } ] },
            "developmentDomains": { "domains": [
              { "id": "66666666-0000-7000-8000-000000000001", "name": "Physical development", "displayOrder": 1, "ratingScaleId": "{{Scale}}",
                "allowsIndicatorComment": true, "sectionId": "{{Section}}", "status": "Active",
                "indicators": [
                  { "id": "{{Indicator}}", "name": "Running", "displayOrder": 1, "status": "Active" },
                  { "id": "66666666-0000-7000-8000-000000000009", "name": "Retired", "displayOrder": 2, "status": "Retired" }
                ] },
              { "id": "66666666-0000-7000-8000-000000000002", "name": "Other section", "displayOrder": 2, "ratingScaleId": "{{Scale}}",
                "allowsIndicatorComment": false, "sectionId": "44444444-0000-7000-8000-000000000099", "status": "Active", "indicators": [] }
            ] },
            "traits": { "traits": [] }
          },
          "section": { "id": "{{Section}}", "name": "Nursery", "ratesTraits": false },
          "arm": { "displayName": "Nursery 2A" },
          "session": { "name": "2026/2027" },
          "term": { "name": "First Term", "ordinal": 1, "endDate": "2026-12-15", "nextResumptionDate": "2027-01-06", "timesSchoolOpened": 58 },
          "subjects": [
            { "subjectId": "{{English}}", "subjectName": "English", "displayOrder": 2 },
            { "subjectId": "{{Maths}}", "subjectName": "Mathematics", "displayOrder": 1 }
          ],
          "formTeacherName": "Mrs Adeyemi"
        }
        """;
}
