using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Movement;

/// <summary>
/// <c>POST /api/v1/pupils/{id}/status/undo</c> (human ruling 2026-09-25): undoes the pupil's most recent status change
/// when it was a leave recorded today, reopening the enrolment it closed. A mistake found later, or a mistaken
/// reactivation, is corrected through the status screen instead.
/// </summary>
/// <param name="Id">The pupil. From the route.</param>
/// <param name="Reason">Optional note on why the change was wrong. At most 500 characters.</param>
public sealed record UndoPupilStatusChangeCommand(Guid Id, string? Reason) : ICommand<Result<PupilMovementOutcomeDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class UndoPupilStatusChangeCommandValidator : AbstractValidator<UndoPupilStatusChangeCommand>
{
    public UndoPupilStatusChangeCommandValidator() =>
        RuleFor(command => command.Reason).MaximumLength(PupilStatusChange.ReasonMaxLength).When(command => command.Reason is not null);
}

/// <summary>Handles <see cref="UndoPupilStatusChangeCommand"/>.</summary>
internal sealed class UndoPupilStatusChangeHandler(
    PupilRecordAccess access,
    IPupilRepository pupils,
    IEnrolmentRepository enrolments,
    IArmRepository arms,
    IPupilStatusChangeRepository statusChanges,
    PupilMovementEngine engine,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UndoPupilStatusChangeCommand, Result<PupilMovementOutcomeDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilMovementOutcomeDto>> HandleAsync(UndoPupilStatusChangeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allowed = await access.CheckAsync(request.Id, Privileges.Pupil.StatusUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(allowed.Error);
        }

        var pupil = await pupils.FindTrackedByIdForUpdateAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (pupil is null)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        var now = timeProvider.GetUtcNow();
        var today = Weekly.WeeklyProjection.LagosToday(now);
        var changes = await statusChanges.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var last = changes.Count == 0 ? null : changes[^1];

        if (last is null
            || last.ToStatus is not (PupilStatus.Transferred or PupilStatus.Withdrawn or PupilStatus.Graduated)
            || pupil.Status != last.ToStatus
            || Weekly.WeeklyProjection.LagosToday(last.ChangedAtUtc) != today)
        {
            return Unavailable();
        }

        // The enrolment the leave closed: the pupil's latest, closed on the leave's effective date, with nothing after it.
        var history = await enrolments.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var latest = history.Count == 0 ? null : history[^1];

        if (history.Any(enrolment => enrolment.IsOpen) || (latest is not null && latest.EffectiveTo != last.EffectiveDate))
        {
            return Unavailable();
        }

        var arm = latest is null ? null : await arms.FindReadOnlyByIdAsync(latest.ArmId, cancellationToken).ConfigureAwait(false);
        var armNames = await engine.DisplayNamesAsync(arm is null ? [] : [arm], cancellationToken).ConfigureAwait(false);

        if (latest is not null)
        {
            var tracked = await enrolments.FindTrackedByIdAsync(latest.Id, cancellationToken).ConfigureAwait(false);
            var reopen = tracked!.Reopen();
            if (reopen.IsFailure)
            {
                return Result.Failure<PupilMovementOutcomeDto>(reopen.Error);
            }
        }

        var reactivate = pupil.Reactivate();
        if (reactivate.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(reactivate.Error);
        }

        var actorId = currentUser.UserId is { } actorIdText && Guid.TryParse(actorIdText, out var parsedActorId) ? parsedActorId : (Guid?)null;
        var note = string.IsNullOrWhiteSpace(request.Reason) ? "Undone: recorded in error." : $"Undone: {request.Reason.Trim()}";
        var row = PupilStatusChange.Create(
            Guid.CreateVersion7(), pupil.Id, last.ToStatus, PupilStatus.Active, last.EffectiveDate, note, arm?.Id, actorId, now);
        if (row.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(row.Error);
        }

        await statusChanges.AddAsync(row.Value, cancellationToken).ConfigureAwait(false);

        var affected = arm is null
            ? []
            : await engine.AssessAsync([arm], armNames, last.EffectiveDate, publishedBlocks: false, cancellationToken).ConfigureAwait(false);
        await engine.ApplyAsync(affected, PupilMovementEngine.CohortNote("an undone status change", today), cancellationToken)
            .ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Pupil.StatusUpdate,
            "pupil",
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = pupil.Status.ToString(),
                ["undo"] = true,
                ["armId"] = arm?.Id.ToString("D", CultureInfo.InvariantCulture),
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["status"] = last.ToStatus.ToString() })
            .ConfigureAwait(false);

        return PupilMovementEngine.Outcome(
            false, pupil, last.ToStatus, PupilStatus.Active, null, arm, armNames, last.EffectiveDate, null, affected, null, today);
    }

    private static Result<PupilMovementOutcomeDto> Unavailable() =>
        Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
            "pupil.status_undo_unavailable",
            "Only a leaving status change recorded today can be undone. Reactivate the pupil from the status screen instead."));
}
