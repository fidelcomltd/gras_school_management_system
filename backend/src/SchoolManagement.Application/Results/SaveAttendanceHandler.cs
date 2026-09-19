using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="SaveAttendanceCommand"/>.</summary>
/// <remarks>
/// <c>result.attendance.enter</c> is the route's ONE declarative privilege (arm-scoped) — spec
/// 6.7.2 also requires the result set to be Draft or Returned for Correction, both DATA-DEPENDENT,
/// so it is enforced here, same "route declares the baseline, handler enforces the data-dependent
/// rest" split <c>SaveTraitRatingsHandler</c> uses. Attendance is NEVER converted to a mark and
/// never computed (spec §6.7.12 amendment) — like a rating save, this handler never calls
/// <see cref="ResultSet.MarkNeedsRecompute"/> on an EXISTING result set.
/// </remarks>
internal sealed class SaveAttendanceHandler(
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    IResultSetRepository resultSets,
    IAttendanceEntryRepository attendanceEntries,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveAttendanceCommand, Result<AttendanceSheetDto>>
{
    /// <summary>The result set exists and is not Draft or Returned for Correction (spec 6.7.2).</summary>
    public const string ResultSetLockedErrorCode = "attendance.result_set_locked";

    /// <summary>The submitted <c>version</c> does not match the sheet's current version.</summary>
    public const string StaleVersionErrorCode = "attendance.stale_version";

    /// <inheritdoc />
    public async Task<Result<AttendanceSheetDto>> HandleAsync(SaveAttendanceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<AttendanceSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<AttendanceSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<AttendanceSheetDto>(Error.Validation(
                "attendance.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(arm.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is { State: SessionState.Closed })
        {
            return Result.Failure<AttendanceSheetDto>(Error.Conflict(
                "attendance.session_closed", "This arm's session is closed. Attendance cannot be entered."));
        }

        if (term.State == TermState.Closed)
        {
            return Result.Failure<AttendanceSheetDto>(Error.Conflict(
                "attendance.term_closed", $"{term.Name} is closed. Attendance cannot be entered."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var rosterPupilIds = roster.Select(pupil => pupil.PupilId).ToHashSet();

        var failures = ValidateRows(request.Rows, rosterPupilIds, term.TimesSchoolOpened);
        if (failures.Count > 0)
        {
            return Result.Failure<AttendanceSheetDto>(new ValidationError(failures));
        }

        var existingResultSet = await resultSets.FindTrackedByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<AttendanceEntry> existingEntries = existingResultSet is null
            ? []
            : await attendanceEntries.ListTrackedAsync(existingResultSet.Id, cancellationToken).ConfigureAwait(false);

        var currentVersion = AttendanceVersion.Compute(existingEntries
            .Select(entry => new AttendanceEntrySnapshot(entry.PupilId, entry.TimesPresent))
            .ToArray());

        // Locked-state check runs BEFORE the staleness check — same ordering rule as
        // SaveTraitRatingsHandler.
        if (existingResultSet is not null &&
            existingResultSet.State is not (ResultSetState.Draft or ResultSetState.ReturnedForCorrection))
        {
            return Result.Failure<AttendanceSheetDto>(Error.Conflict(
                ResultSetLockedErrorCode, $"This result set is {existingResultSet.State} and attendance cannot be edited."));
        }

        if (!string.Equals(request.Version, currentVersion, StringComparison.Ordinal))
        {
            return Result.Failure<AttendanceSheetDto>(Error.Conflict(
                StaleVersionErrorCode, "This sheet was changed since you last read it. Reload it before saving again."));
        }

        var byPupil = existingEntries.ToDictionary(entry => entry.PupilId);
        var resultSetRef = existingResultSet;

        var beforeChanges = new List<object?>();
        var afterChanges = new List<object?>();

        foreach (var row in request.Rows)
        {
            var pupilId = Guid.Parse(row.PupilId);
            var hasExisting = byPupil.TryGetValue(pupilId, out var existing);

            if (row.TimesPresent is null)
            {
                if (hasExisting)
                {
                    beforeChanges.Add(Snapshot(pupilId, existing!.TimesPresent));
                    afterChanges.Add(Snapshot(pupilId, timesPresent: null));
                    await attendanceEntries.RemoveAsync(existing!, cancellationToken).ConfigureAwait(false);
                    byPupil.Remove(pupilId);
                }

                continue;
            }

            var timesPresent = row.TimesPresent.Value;

            if (hasExisting)
            {
                beforeChanges.Add(Snapshot(pupilId, existing!.TimesPresent));
                existing.UpdateTimesPresent(timesPresent);
            }
            else
            {
                beforeChanges.Add(Snapshot(pupilId, timesPresent: null));

                if (resultSetRef is null)
                {
                    var creation = ResultSet.Create(Guid.CreateVersion7(), armId, termId);
                    if (creation.IsFailure)
                    {
                        return Result.Failure<AttendanceSheetDto>(creation.Error);
                    }

                    resultSetRef = creation.Value;
                    await resultSets.AddAsync(resultSetRef, cancellationToken).ConfigureAwait(false);
                }

                var entryCreation = AttendanceEntry.Create(Guid.CreateVersion7(), resultSetRef.Id, pupilId, timesPresent);
                if (entryCreation.IsFailure)
                {
                    return Result.Failure<AttendanceSheetDto>(entryCreation.Error);
                }

                existing = entryCreation.Value;
                await attendanceEntries.AddAsync(existing, cancellationToken).ConfigureAwait(false);
                byPupil[pupilId] = existing;
            }

            afterChanges.Add(Snapshot(pupilId, timesPresent));
        }

        if (afterChanges.Count > 0)
        {
            await auditSink.RecordAsync(
                Privileges.Results.AttendanceEnter,
                "attendance_entry",
                entityId: null,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["changes"] = afterChanges },
                currentUser.UserId,
                cancellationToken,
                reason: null,
                beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["armId"] = request.ArmId, ["termId"] = request.TermId, ["changes"] = beforeChanges })
                .ConfigureAwait(false);
        }

        var finalEntries = byPupil.Values
            .Select(entry => new AttendanceEntrySnapshot(entry.PupilId, entry.TimesPresent))
            .ToArray();

        var dto = AttendanceProjection.Build(armId, termId, term.TimesSchoolOpened, resultSetRef, roster, finalEntries);

        return Result.Success(dto);
    }

    private static Dictionary<string, object?> Snapshot(Guid pupilId, int? timesPresent) => new(StringComparer.Ordinal)
    {
        ["pupilId"] = pupilId.ToString("D", CultureInfo.InvariantCulture),
        ["timesPresent"] = timesPresent,
    };

    private static Dictionary<string, string[]> ValidateRows(
        IReadOnlyList<SaveAttendanceRowInput> rows, HashSet<Guid> rosterPupilIds, int? timesSchoolOpened)
    {
        var failures = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void AddFailure(string path, string message)
        {
            if (!failures.TryGetValue(path, out var list))
            {
                list = [];
                failures[path] = list;
            }

            if (!list.Contains(message, StringComparer.Ordinal))
            {
                list.Add(message);
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            if (!Guid.TryParse(row.PupilId, out var pupilId) || !rosterPupilIds.Contains(pupilId))
            {
                AddFailure($"Rows[{index}].PupilId", "This pupil is not on the arm's active roster.");
                continue;
            }

            if (row.TimesPresent is null)
            {
                continue;
            }

            var timesPresent = row.TimesPresent.Value;

            if (timesPresent < AttendanceEntry.MinTimesPresent)
            {
                AddFailure($"Rows[{index}].TimesPresent", "Times present must not be negative.");
                continue;
            }

            // The upper bound is the term's own maximum (spec §6.7.7 amendment's example: "24 present
            // plus 3 absent is 27. School opened 58 times this term") when it is set, else 200 — the
            // term field's own upper bound (Term.MaxTimesSchoolOpened) — while it is still blank.
            var upperBound = timesSchoolOpened ?? Term.MaxTimesSchoolOpened;
            if (timesPresent > upperBound)
            {
                AddFailure($"Rows[{index}].TimesPresent", $"Times present cannot exceed {upperBound}.");
            }
        }

        return failures.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal);
    }
}
