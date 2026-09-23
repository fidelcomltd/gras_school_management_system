using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Weekly;

/// <summary>One day panel of a weekly report (spec 6.10.6): eight free-text lines, any of them null.</summary>
/// <param name="DayOfWeek">Monday to Friday.</param>
/// <param name="Date">The calendar date.</param>
/// <param name="Behaviour">Behaviour line.</param>
/// <param name="Performance">Performance line.</param>
/// <param name="Dressing">Dressing line.</param>
/// <param name="HomeWork">Home Work line.</param>
/// <param name="Eating">Eating line.</param>
/// <param name="SymptomsOfIllness">Symptoms of illness line.</param>
/// <param name="TeacherComment">Teacher's Comment line.</param>
/// <param name="ParentComment">Parent's Comment line, transcribed by staff.</param>
/// <param name="LastEditedAt">When this day was last written; null when never.</param>
/// <param name="LastEditedById">The account that last wrote it.</param>
/// <param name="LastEditedBy">That account's staff name. Spec 6.10.10 shows it beneath the cell when within the last hour.</param>
public sealed record WeeklyDayDto(
    WeeklyDay DayOfWeek,
    DateOnly Date,
    string? Behaviour,
    string? Performance,
    string? Dressing,
    string? HomeWork,
    string? Eating,
    string? SymptomsOfIllness,
    string? TeacherComment,
    string? ParentComment,
    DateTimeOffset? LastEditedAt,
    string? LastEditedById,
    string? LastEditedBy);

/// <summary>One pupil's week on the arm grid.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="RegistrationNumber">Null only if unissued.</param>
/// <param name="DisplayName">"Surname First Middle".</param>
/// <param name="OnRoll">False for a pupil who has notes in this week but has since left the arm; still editable.</param>
/// <param name="IllnessDays">Days this week with a symptoms note. Two or more shows the quiet marker (spec 6.10.6).</param>
/// <param name="Days">Always five, Monday first, blank where nothing is written.</param>
public sealed record WeeklyGridRowDto(
    string PupilId, string? RegistrationNumber, string DisplayName, bool OnRoll, int IllnessDays, IReadOnlyList<WeeklyDayDto> Days);

/// <summary>One week of the term, as the arm sees it.</summary>
/// <param name="WeekNumber">1 to 20.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
/// <param name="OutsideTerm">The week holds notes but falls outside the term's current dates (spec 6.10.10).</param>
/// <param name="Published">Visible to parents.</param>
/// <param name="PupilsWithNotes">Pupils with at least one note.</param>
public sealed record WeeklyWeekSummaryDto(int WeekNumber, DateOnly StartDate, DateOnly EndDate, bool OutsideTerm, bool Published, int PupilsWithNotes);

/// <summary>Phrase memory (spec 6.10.7): what the signed-in account already wrote this term per line, most recent first.</summary>
/// <param name="Behaviour">Behaviour line.</param>
/// <param name="Performance">Performance line.</param>
/// <param name="Dressing">Dressing line.</param>
/// <param name="HomeWork">Home Work line.</param>
/// <param name="Eating">Eating line.</param>
/// <param name="SymptomsOfIllness">Symptoms of illness line.</param>
/// <param name="TeacherComment">Teacher's Comment line.</param>
/// <param name="ParentComment">Parent's Comment line.</param>
public sealed record WeeklyPhrasesDto(
    IReadOnlyList<string> Behaviour,
    IReadOnlyList<string> Performance,
    IReadOnlyList<string> Dressing,
    IReadOnlyList<string> HomeWork,
    IReadOnlyList<string> Eating,
    IReadOnlyList<string> SymptomsOfIllness,
    IReadOnlyList<string> TeacherComment,
    IReadOnlyList<string> ParentComment);

/// <summary>One arm's weekly grid for one week (spec 6.10.7, 6.10.11): pupils down the side, weekdays across.</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="TermId">The term.</param>
/// <param name="WeekNumber">The week shown.</param>
/// <param name="WeekStartDate">Its Monday.</param>
/// <param name="WeekEndDate">Its Friday.</param>
/// <param name="OutsideTerm">The week falls outside the term's current dates; its notes are retained.</param>
/// <param name="Published">Visible to parents.</param>
/// <param name="PublishedAt">When it was published.</param>
/// <param name="AutoPublish">The arm publishes each week at 17:00 on its Friday.</param>
/// <param name="Locked">The term or its session is closed: notes can be read but not written (409 on save).</param>
/// <param name="Rows">Every active pupil, plus any pupil with notes in this week who has since left, surname order.</param>
/// <param name="Weeks">Every week of the term for this arm, for the week picker.</param>
/// <param name="Phrases">The signed-in account's own phrases this term.</param>
public sealed record WeeklyGridDto(
    string ArmId,
    string TermId,
    int WeekNumber,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    bool OutsideTerm,
    bool Published,
    DateTimeOffset? PublishedAt,
    bool AutoPublish,
    bool Locked,
    IReadOnlyList<WeeklyGridRowDto> Rows,
    IReadOnlyList<WeeklyWeekSummaryDto> Weeks,
    WeeklyPhrasesDto Phrases);

/// <summary>One derived week of a term (spec 6.10.3).</summary>
/// <param name="WeekNumber">1 to 20.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
public sealed record TermWeekDto(int WeekNumber, DateOnly StartDate, DateOnly EndDate);

/// <summary>A term's derived weeks. Bounded at 20, so not paginated.</summary>
/// <param name="TermId">The term.</param>
/// <param name="Items">In order.</param>
public sealed record TermWeekListResponse(string TermId, IReadOnlyList<TermWeekDto> Items);

/// <summary>One week of a pupil's term.</summary>
/// <param name="WeekNumber">1 to 20.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
/// <param name="OutsideTerm">Holds notes but falls outside the term's current dates.</param>
/// <param name="ArmId">The arm the week is attributed to; null when nothing is written.</param>
/// <param name="Published">Visible to parents.</param>
/// <param name="Days">Five days, or null when the pupil has no report for the week.</param>
public sealed record PupilWeeklyWeekDto(
    int WeekNumber, DateOnly StartDate, DateOnly EndDate, bool OutsideTerm, string? ArmId, bool Published, IReadOnlyList<WeeklyDayDto>? Days);

/// <summary>One pupil's whole term, week by week (spec 6.10.11). Backs the per-pupil tab.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="RegistrationNumber">Null only if unissued.</param>
/// <param name="DisplayName">"Surname First Middle".</param>
/// <param name="TermId">The term.</param>
/// <param name="Weeks">One row per week of the term, plus any outside-term week that holds notes.</param>
public sealed record PupilWeeklyTermDto(
    string PupilId, string? RegistrationNumber, string DisplayName, string TermId, IReadOnlyList<PupilWeeklyWeekDto> Weeks);

/// <summary>An arm's weekly-report settings (spec 6.10.8).</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="AutoPublish">Publish each week automatically at 17:00 on its Friday.</param>
public sealed record WeeklySettingsDto(string ArmId, bool AutoPublish);

/// <summary>One arm's week on the completion report (spec 6.10.12).</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="ArmName">Level plus arm label.</param>
/// <param name="WeekNumber">The week.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
/// <param name="PupilsOnRoll">The arm's current active roster.</param>
/// <param name="PupilsWithNotes">Pupils with at least one note.</param>
/// <param name="CellsFilled">Non-empty lines written.</param>
/// <param name="CellsAvailable">Pupils on roll times five days times eight lines.</param>
/// <param name="Published">Visible to parents.</param>
/// <param name="LastEditedAt">The latest edit, or null.</param>
/// <param name="LastEditedBy">Who made it.</param>
public sealed record WeeklyCompletionRowDto(
    string ArmId, string ArmName, int WeekNumber, DateOnly StartDate, DateOnly EndDate, int PupilsOnRoll, int PupilsWithNotes,
    int CellsFilled, int CellsAvailable, bool Published, DateTimeOffset? LastEditedAt, string? LastEditedBy);

/// <summary>The weekly report completion report for a term. Bounded by arms times 20 weeks, so not paginated.</summary>
/// <param name="TermId">The term.</param>
/// <param name="Items">Arm name, then week.</param>
public sealed record WeeklyCompletionReportDto(string TermId, IReadOnlyList<WeeklyCompletionRowDto> Items);

/// <summary>One recorded observation.</summary>
/// <param name="Date">The day.</param>
/// <param name="Text">The symptoms line as written.</param>
public sealed record WeeklyIllnessObservationDto(DateOnly Date, string Text);

/// <summary>A pupil with symptoms recorded on two or more days of the term (spec 6.10.12).</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="RegistrationNumber">Null only if unissued.</param>
/// <param name="DisplayName">"Surname First Middle".</param>
/// <param name="ArmName">The arm of the latest observation.</param>
/// <param name="Observations">In date order.</param>
public sealed record WeeklyIllnessRowDto(
    string PupilId, string? RegistrationNumber, string DisplayName, string ArmName, IReadOnlyList<WeeklyIllnessObservationDto> Observations);

/// <summary>The illness observation summary for a term. Health observation about children: safeguarding privilege only.</summary>
/// <param name="TermId">The term.</param>
/// <param name="Items">Pupil display name order.</param>
public sealed record WeeklyIllnessReportDto(string TermId, IReadOnlyList<WeeklyIllnessRowDto> Items);
