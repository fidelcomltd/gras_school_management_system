using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>Handles <see cref="UpdateTermCommand"/>.</summary>
internal sealed class UpdateTermHandler(
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IAttendanceEntryRepository attendanceEntries,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateTermCommand, Result<TermDto>>
{
    /// <inheritdoc />
    public async Task<Result<TermDto>> HandleAsync(UpdateTermCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var term = await terms.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<TermDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var (previousStart, previousEnd) = (term.StartDate, term.EndDate);
        var touchesSchedule = request.Name is not null
            || request.StartDate is not null
            || request.EndDate is not null
            || request.NextResumptionDate is not null;

        if (touchesSchedule)
        {
            var session = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);

            if (session is null)
            {
                return Result.Failure<TermDto>(Error.Failure(
                    "term.orphaned",
                    "This term's session could not be found."));
            }

            var effectiveName = request.Name ?? term.Name;
            var effectiveStart = request.StartDate ?? term.StartDate;
            var effectiveEnd = request.EndDate ?? term.EndDate;
            var effectiveResumption = request.NextResumptionDate ?? term.NextResumptionDate;

            var withinSession = TermChronologyGuard.ValidateWithinSession(
                session.Name, session.StartDate, session.EndDate, effectiveName, effectiveStart, effectiveEnd);

            if (withinSession.IsFailure)
            {
                return Result.Failure<TermDto>(withinSession.Error);
            }

            if (term.Ordinal > 1)
            {
                var previous = await terms
                    .FindByOrdinalAsync(session.Id, term.Ordinal - 1, cancellationToken)
                    .ConfigureAwait(false);

                if (previous is not null)
                {
                    var sequential = TermChronologyGuard.ValidateSequential(
                        previous.Name, previous.EndDate, effectiveName, effectiveStart);

                    if (sequential.IsFailure)
                    {
                        return Result.Failure<TermDto>(sequential.Error);
                    }
                }
            }

            if (term.Ordinal < 3)
            {
                var next = await terms
                    .FindByOrdinalAsync(session.Id, term.Ordinal + 1, cancellationToken)
                    .ConfigureAwait(false);

                if (next is not null)
                {
                    var sequential = TermChronologyGuard.ValidateSequential(
                        effectiveName, effectiveEnd, next.Name, next.StartDate);

                    if (sequential.IsFailure)
                    {
                        return Result.Failure<TermDto>(sequential.Error);
                    }
                }
            }

            var update = term.UpdateSchedule(effectiveName, effectiveStart, effectiveEnd, effectiveResumption);

            if (update.IsFailure)
            {
                return Result.Failure<TermDto>(update.Error);
            }
        }

        if (request.TimesSchoolOpened is { } timesSchoolOpened)
        {
            // TASK-0086 delta item 5: refuse to drop times_school_opened below the highest
            // times_present already recorded for this term (any arm) — the derived times-absent
            // (attendance ruling A) would otherwise go negative for whichever pupil holds that
            // record.
            var maxTimesPresent = await attendanceEntries
                .FindMaxTimesPresentByTermAsync(term.Id, cancellationToken)
                .ConfigureAwait(false);

            if (maxTimesPresent is { } recordedMax && timesSchoolOpened < recordedMax)
            {
                return Result.Failure<TermDto>(Error.Conflict(
                    "term.times_school_opened_below_attendance",
                    $"Times school opened cannot be set below {recordedMax}, the highest times present " +
                    $"already recorded for {term.Name}."));
            }

            var setResult = term.SetTimesSchoolOpened(timesSchoolOpened);

            if (setResult.IsFailure)
            {
                return Result.Failure<TermDto>(setResult.Error);
            }
        }

        await FollowSuggestedTimesSchoolOpenedAsync(term, request, previousStart, previousEnd, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Session.Update,
            "term",
            term.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SessionMapper.ToTermDto(term));
    }

    /// <summary>
    /// A term's times school opened starts as its weekdays (<see cref="Term.SuggestTimesSchoolOpened"/>). While it still
    /// equals the suggestion for the dates it had, nobody has corrected it, so it follows a change of dates; a figure the
    /// school entered is never touched. Skipped, never refused, when it would fall below recorded times present or the
    /// term is closed: the date edit itself still succeeds.
    /// </summary>
    private async Task FollowSuggestedTimesSchoolOpenedAsync(
        Term term, UpdateTermCommand request, DateOnly previousStart, DateOnly previousEnd, CancellationToken cancellationToken)
    {
        if (request.TimesSchoolOpened is not null
            || (term.StartDate == previousStart && term.EndDate == previousEnd)
            || term.TimesSchoolOpened is not { } current
            || current != Term.SuggestTimesSchoolOpened(previousStart, previousEnd)
            || Term.SuggestTimesSchoolOpened(term.StartDate, term.EndDate) is not { } fresh
            || fresh == current)
        {
            return;
        }

        var maxTimesPresent = await attendanceEntries.FindMaxTimesPresentByTermAsync(term.Id, cancellationToken).ConfigureAwait(false);
        if (maxTimesPresent is null || fresh >= maxTimesPresent)
        {
            _ = term.SetTimesSchoolOpened(fresh);
        }
    }
}
