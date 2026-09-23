using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Weekly;
using SchoolManagement.Infrastructure.Weekly;

namespace SchoolManagement.UnitTests.Domain.Weekly;

/// <summary>Spec 6.10: week derivation, the report's note rules, and the auto-publish window.</summary>
public sealed class WeeklyReportTests
{
    [Fact]
    public void Derive_TermStartingMidWeek_BeginsOnThatMondayAndEndsOnTheWeekContainingTheEndDate()
    {
        // Wednesday 6 January 2027 to Tuesday 6 April 2027.
        var weeks = TermWeeks.Derive(new DateOnly(2027, 1, 6), new DateOnly(2027, 4, 6));

        weeks[0].ShouldBe(new TermWeek(1, new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 8)));
        weeks[^1].StartDate.ShouldBe(new DateOnly(2027, 4, 5));
        weeks.Count.ShouldBe(14);
        weeks.Select(week => week.Number).ShouldBe(Enumerable.Range(1, 14));
    }

    [Fact]
    public void Derive_NeverExceedsTwentyWeeks()
    {
        TermWeeks.Derive(new DateOnly(2027, 1, 4), new DateOnly(2027, 12, 31)).Count.ShouldBe(TermWeeks.MaxWeeks);
    }

    [Fact]
    public void Create_MakesFiveDatedDays_AndInheritsAPublishedWeek()
    {
        var week = new TermWeek(4, new DateOnly(2027, 1, 25), new DateOnly(2027, 1, 29));

        var report = WeeklyReport.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), week, weekIsPublished: true, DateTimeOffset.UnixEpoch).Value;

        report.Days.Select(day => day.DayOfWeek).ShouldBe(Enum.GetValues<WeeklyDay>());
        report.Day(WeeklyDay.Friday).ReportDate.ShouldBe(new DateOnly(2027, 1, 29));
        report.State.ShouldBe(WeeklyReportState.Published);
        report.HasContent.ShouldBeFalse();
    }

    [Fact]
    public void SetNote_TrimsBlankToNull_AndOnlyBumpsTheRevisionOnAChange()
    {
        var report = NewReport();

        report.SetNote(WeeklyDay.Monday, WeeklyField.Eating, "  Ate everything ").ShouldBeTrue();
        report.SetNote(WeeklyDay.Monday, WeeklyField.Eating, "Ate everything").ShouldBeFalse();
        report.Day(WeeklyDay.Monday).Eating.ShouldBe("Ate everything");
        report.Revision.ShouldBe(1);
        report.HasContent.ShouldBeTrue();

        report.SetNote(WeeklyDay.Monday, WeeklyField.Eating, "   ").ShouldBeTrue();
        report.Day(WeeklyDay.Monday).Eating.ShouldBeNull();
        report.HasContent.ShouldBeFalse();
    }

    [Fact]
    public void Validator_RejectsALineOverItsLimit_ButAllowsFiveHundredForAComment()
    {
        var validator = new SaveWeeklyNotesCommandValidator();
        var pupil = Guid.CreateVersion7().ToString();

        SaveWeeklyNotesCommand Command(WeeklyField field, int length) =>
            new(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), 1, [new WeeklyCellInput(pupil, WeeklyDay.Monday, field, new string('a', length))]);

        validator.Validate(Command(WeeklyField.Eating, 301)).IsValid.ShouldBeFalse();
        validator.Validate(Command(WeeklyField.TeacherComment, 500)).IsValid.ShouldBeTrue();
        validator.Validate(Command(WeeklyField.ParentComment, 501)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2027-01-15T15:59:00Z", null)] // Friday 16:59 Lagos: not yet.
    [InlineData("2027-01-15T16:00:00Z", "2027-01-11")] // Friday 17:00 Lagos.
    [InlineData("2027-01-17T22:00:00Z", "2027-01-11")] // Sunday 23:00 Lagos: still catching up.
    [InlineData("2027-01-17T23:00:00Z", null)] // Monday 00:00 Lagos: too late.
    [InlineData("2027-01-13T12:00:00Z", null)] // Wednesday.
    public void DueWeekStart_IsFridaySeventeenHundredLagosUntilSunday(string now, string? expected)
    {
        WeeklyAutoPublishService.DueWeekStart(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(expected is null ? null : DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Weeks_FlagsAStoredWeekThatNoLongerMatchesTheTermsDates()
    {
        var derived = TermWeeks.Derive(new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 22));
        WeeklyArmWeekTotals Totals(int number, DateOnly start) => new(Guid.Empty, number, start, start.AddDays(4), 3, 10, false, null, null);

        var weeks = WeeklyProjection.Weeks(derived, [Totals(2, new DateOnly(2027, 1, 11)), Totals(5, new DateOnly(2027, 2, 1))]);

        weeks.Count.ShouldBe(4);
        weeks[1].OutsideTerm.ShouldBeFalse();
        weeks[1].PupilsWithNotes.ShouldBe(3);
        weeks[3].ShouldBe(new WeeklyWeekSummaryDto(5, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 5), true, false, 3));
    }

    private static WeeklyReport NewReport() =>
        WeeklyReport.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            new TermWeek(1, new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 8)), weekIsPublished: false, null).Value;
}
