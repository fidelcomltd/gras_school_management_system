namespace SchoolManagement.Application.Results;

/// <summary>
/// One submitted row shared by <c>SaveClassTeacherRemarksCommand</c> and
/// <c>SaveHeadTeacherRemarksCommand</c> (TASK-0086 stage A). A pupil OMITTED from a command's
/// <c>Rows</c> leaves that pupil's existing remark UNTOUCHED (Q1-A's convention, generalised to a
/// single-value field, same as <see cref="SaveAttendanceRowInput"/>). A pupil present with a
/// <see langword="null"/> or empty/whitespace-only <see cref="Remark"/> CLEARS (deletes) the
/// remark. A pupil present with non-blank text sets or replaces it, trimmed.
/// </summary>
/// <param name="PupilId">Must be on the arm's active roster.</param>
/// <param name="Remark"><see langword="null"/>, empty or whitespace-only clears.</param>
public sealed record SaveRemarkRowInput(string PupilId, string? Remark);
