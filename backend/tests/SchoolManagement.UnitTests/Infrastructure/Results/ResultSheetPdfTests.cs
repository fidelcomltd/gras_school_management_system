using Microsoft.Extensions.Options;
using NSubstitute;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Portal;
using SchoolManagement.Application.Results.Sheets;
using SchoolManagement.Domain.Results;
using SchoolManagement.Infrastructure.Pins;
using SchoolManagement.Infrastructure.Results;

namespace SchoolManagement.UnitTests.Infrastructure.Results;

/// <summary>Portal part 3c: the A4 sheet's size budget and verification marks, and the verification page's rules.</summary>
public sealed class ResultSheetPdfTests
{
    private const string Token = "ABCDEFGHJKMNPQRSTUVWXY";

    [Fact]
    public void AFullPrimarySheet_WithTheVerificationMarks_IsAPdfUnder200Kb()
    {
        var renderer = new QuestPdfResultSheetRenderer(Options.Create(new PortalOptions { PublicUrl = "results.example.sch.ng" }));
        var url = renderer.VerificationUrl(Token);
        url!.AbsoluteUri.ShouldBe($"https://results.example.sch.ng/verify/{Token}");

        var pdf = renderer.Render(Sheet(subjects: 14), new ResultSheetPdfExtras(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Token, url, DateTimeOffset.UtcNow));

        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        pdf.Length.ShouldBeLessThan(200 * 1024);
    }

    [Fact]
    public void ALongSubjectList_FlowsOntoASecondPage_WithoutFailing()
    {
        var renderer = new QuestPdfResultSheetRenderer(Options.Create(new PortalOptions()));
        renderer.VerificationUrl(Token).ShouldBeNull();

        var pdf = renderer.Render(Sheet(subjects: 30), new ResultSheetPdfExtras(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, null, null, DateTimeOffset.UtcNow));

        pdf.Length.ShouldBeLessThan(200 * 1024);
    }

    [Fact]
    public void AnnualSheet_ReadsTheRowAndSnapshot_OrdersSubjects_AndRendersUnder200Kb()
    {
        var english = Guid.Parse("22222222-0000-7000-8000-000000000001");
        var maths = Guid.Parse("22222222-0000-7000-8000-000000000002");
        var french = Guid.Parse("22222222-0000-7000-8000-000000000003");
        var row = AnnualResult.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, [null, 78.25m, 82.50m], [null, 313, 330], 643, 80.38m, "B", "Very good", 1, false, 27,
            $$"""[{"subjectId":"{{french}}","termTotals":[null,60,null],"mean":60,"grade":"C","termsTaken":1},{"subjectId":"{{maths}}","termTotals":[null,71,80],"mean":75.5,"grade":"C","termsTaken":2},{"subjectId":"{{english}}","termTotals":[null,82,88],"mean":85,"grade":"B","termsTaken":2}]""",
            PromotionOutcome.Promoted, DateTimeOffset.UtcNow);
        var snapshot = $$$"""{"settings":{"identity":{"schoolName":"Golden Royal Ark School","headTeacherName":"Mrs N. Okonkwo"},"grading":{"bands":[{"gradeLetter":"B","lowerBound":80,"upperBound":89,"remark":"Very good","displayOrder":1}]}},"arm":{"displayName":"Primary 3A"},"session":{"name":"2026/2027"},"subjects":[{"subjectId":"{{{maths}}}","subjectName":"Mathematics","displayOrder":2},{"subjectId":"{{{english}}}","subjectName":"English","displayOrder":1}]}""";

        var sheet = AnnualSheetBuilder.Build(new AnnualSheetData(row, snapshot, "Okafor", "Adaeze", "GRAS/2024/0087",
            new Dictionary<Guid, string> { [english] = "English", [maths] = "Mathematics", [french] = "French" }))!;

        sheet.Subjects.Select(subject => subject.Subject).ShouldBe(["English", "Mathematics", "French"]);
        sheet.TermsCountedNote.ShouldBe("Cumulative average based on 2 of 3 terms.");
        sheet.ClassName.ShouldBe("Primary 3A");
        sheet.PupilName.ShouldBe("OKAFOR Adaeze");
        AnnualSheetBuilder.Build(new AnnualSheetData(row, null, "Okafor", "Adaeze", "x", new Dictionary<Guid, string>())).ShouldBeNull();

        var pdf = new QuestPdfResultSheetRenderer(Options.Create(new PortalOptions()))
            .RenderAnnual(sheet, new ResultSheetPdfExtras(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, null, null, DateTimeOffset.UtcNow));
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        pdf.Length.ShouldBeLessThan(200 * 1024);
    }

    [Fact]
    public void Tokens_PrintInGroupsOfFive() => ResultVerification.Format(Token).ShouldBe("ABCDE FGHJK MNPQR STUVW XY");

    [Fact]
    public void Tokens_AreFresh_AndDrawnFromThePinAlphabet()
    {
        var first = ResultVerification.Issue(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow).Token;
        var second = ResultVerification.Issue(Guid.NewGuid(), Guid.NewGuid(), 1, DateTimeOffset.UtcNow).Token;
        first.Length.ShouldBe(22);
        first.ShouldNotBe(second);
        first.ShouldAllBe(character => SchoolManagement.Domain.Pins.PinValue.Alphabet.Contains(character));
    }

    [Theory]
    [InlineData("GRAS/2026/0041", "First Term", "2026/2027", "GRAS-2026-0041_First-Term_2026-2027.pdf")]
    [InlineData("  ", "Third Term", "2026/2027", "result_Third-Term_2026-2027.pdf")]
    public void FileNames_AreSafeOnAndroidAndWindows(string number, string term, string session, string expected) =>
        GetPortalResultPdfHandler.FileName(number, term, session).ShouldBe(expected);

    [Theory]
    [InlineData(ResultSetState.Published, 1, ResultVerificationStatus.Current, true)]
    [InlineData(ResultSetState.Published, 2, ResultVerificationStatus.Revised, false)]
    [InlineData(ResultSetState.Withdrawn, 1, ResultVerificationStatus.Withdrawn, false)]
    [InlineData(ResultSetState.Draft, 1, ResultVerificationStatus.Withdrawn, false)]
    public async Task Verification_ShowsFigures_OnlyWhileTheSheetsRevisionIsCurrent(ResultSetState state, int currentRevision, ResultVerificationStatus expected, bool figures)
    {
        var reader = Substitute.For<IResultVerificationReader>();
        reader.FindAsync(Token, Arg.Any<CancellationToken>()).Returns(new ResultVerificationRecord(
            1, DateTimeOffset.UtcNow, """{"settings":{"identity":{"schoolName":"Golden Royal Ark School"}},"arm":{"displayName":"Primary 4A"},"term":{"name":"First Term"},"session":{"name":"2026/2027"}}""",
            state, currentRevision, DateTimeOffset.UtcNow, "Okafor", "Chidera Amaka", "GRAS/2026/0041", 612, 900, 68.00m, "B"));

        var view = (await new VerifyResultHandler(reader).HandleAsync(new VerifyResultQuery("abcde-fghjk mnpqr stuvw xy"), TestContext.Current.CancellationToken)).Value;

        view.Status.ShouldBe(expected);
        view.Initials.ShouldBe("C. A. O.");
        view.ArmName.ShouldBe("Primary 4A");
        (view.Average is not null).ShouldBe(figures);
        (view.RevisedAt is not null).ShouldBe(expected == ResultVerificationStatus.Revised);
    }

    [Theory]
    [InlineData("TOO-SHORT")]
    [InlineData("ABCDEFGHJKMNPQRSTUVWX0")] // 0 is outside the alphabet
    public async Task Verification_AMalformedCode_IsUnknown_WithoutAReadAsync(string code)
    {
        var reader = Substitute.For<IResultVerificationReader>();

        var view = (await new VerifyResultHandler(reader).HandleAsync(new VerifyResultQuery(code), TestContext.Current.CancellationToken)).Value;

        view.Status.ShouldBe(ResultVerificationStatus.Unknown);
        await reader.DidNotReceiveWithAnyArgs().FindAsync(default!, TestContext.Current.CancellationToken);
    }

    private static ResultSheet Sheet(int subjects)
    {
        SheetColumn[] columns = [new("1st CA", 20, false), new("2nd CA", 20, false), new("Exam", 60, true)];
        var rows = Enumerable.Range(1, subjects)
            .Select(index => new SheetSubjectRow($"Subject number {index}", ["15", index % 5 == 0 ? null : "14", index % 7 == 0 ? "ABS" : "41"], 70, "B", "Very Good"))
            .ToList();
        var traits = new SheetRatingBlock("Affective Domain", ["5", "4", "3", "2", "1"], false,
            Enumerable.Range(1, 7).Select(index => new SheetRatingItem($"Trait {index}", "4", null)).ToList());
        var skills = new SheetRatingBlock("Psychomotor", ["5", "4", "3", "2", "1"], false,
            Enumerable.Range(1, 5).Select(index => new SheetRatingItem($"Skill {index}", "3", null)).ToList());
        return new ResultSheet(
            SheetSection.Primary, "Golden Royal Ark School", null, null, "OKAFOR Chidera Amaka", "GRAS/2026/0041", 9, "2026/2027", "First Term",
            "Primary 4A, First Term", "Mrs Adeyemi", new DateOnly(2027, 1, 6), "B", 70.00m, null, columns, rows,
            [15 * subjects, 14 * subjects, 41 * subjects, 70 * subjects], [traits, skills], "5-Excellent  4-Very good  3-Good  2-Fair  1-Poor",
            new SheetAttendance(58, 55, 3), "A steady term.", "Keep working hard.",
            [new SheetGradeKeyRow("A", "80-100", "Excellent"), new SheetGradeKeyRow("B", "70-79", "Very Good")],
            DateTimeOffset.UtcNow, "14 Zik Avenue\nOwerri", "Knowledge and Light", "Mrs N. Okonkwo", 2);
    }
}
