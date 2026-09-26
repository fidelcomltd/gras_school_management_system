using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Classes;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Classes;
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
/// <param name="Reason">Optional note on why the change was wrong. At most 492 characters: it is stored after "Undone: ".</param>
public sealed record UndoPupilStatusChangeCommand(Guid Id, string? Reason) : ICommand<Result<PupilMovementOutcomeDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class UndoPupilStatusChangeCommandValidator : AbstractValidator<UndoPupilStatusChangeCommand>
{
    /// <summary>Prefixed to the stored reason.</summary>
    public const string NotePrefix = "Undone: ";

    public UndoPupilStatusChangeCommandValidator() =>
        RuleFor(command => command.Reason)
            .MaximumLength(PupilStatusChange.ReasonMaxLength - NotePrefix.Length)
            .When(command => command.Reason is not null);
}

/// <summary>Handles <see cref="UndoPupilStatusChangeCommand"/>.</summary>
internal sealed class UndoPupilStatusChangeHandler(
    PupilRecordAccess access,
    IPupilRepository pupils,
    IEnrolmentRepository enrolments,
    IArmRepository arms,
    IPupilStatusChangeRepository statusChanges,
    PupilMovementEngine engine,
    ArmCapacityGuard capacityGuard,
    TimeProvider timeProvider)
    : IRequestHandler<UndoPupilStatusChangeCommand, Result<PupilMovementOutcomeDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilMovementOutcomeDto>> HandleAsync(UndoPupilStatusChangeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pupil = await pupils.FindTrackedByIdForUpdateAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (pupil is null)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        var today = Weekly.WeeklyProjection.LagosToday(timeProvider.GetUtcNow());
        var changes = await statusChanges.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var last = changes.Count == 0 ? null : changes[^1];

        // Only a leave recorded today that closed an enrolment (its row names the arm left). An older leave row carries no
        // arm, and is refused rather than guessed at.
        if (last is not { ArmId: { } leftArmId }
            || last.ToStatus is not (PupilStatus.Transferred or PupilStatus.Withdrawn or PupilStatus.Graduated)
            || pupil.Status != last.ToStatus
            || Weekly.WeeklyProjection.LagosToday(last.ChangedAtUtc) != today)
        {
            return await RefuseAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        }

        // The pupil is a leaver, so the arm-scope check is against the arm being reopened, not an open enrolment.
        var allowed = await access.CheckArmAsync(leftArmId, Privileges.Pupil.StatusUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(allowed.Error);
        }

        // The enrolment that leave closed: the pupil's latest, in that arm, closed on the leave's date, nothing after it.
        var history = await enrolments.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var latest = history.Count == 0 ? null : history[^1];
        if (latest is null || latest.IsOpen || latest.ArmId != leftArmId || latest.EffectiveTo != last.EffectiveDate)
        {
            return Unavailable();
        }

        // Reopening puts the pupil back into a class, so it runs reactivation's arm checks: still active, and capacity.
        var arm = await arms.FindReadOnlyByIdAsync(leftArmId, cancellationToken).ConfigureAwait(false);
        if (arm is null || arm.Status != ArmStatus.Active)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
                "pupil.status_undo_arm_unavailable", "The pupil's class is no longer active, so the change cannot be undone. Reactivate them into an active class instead."));
        }

        var armNames = await engine.DisplayNamesAsync([arm], cancellationToken).ConfigureAwait(false);
        var capacity = await capacityGuard.CheckAsync(arm, cancellationToken).ConfigureAwait(false);
        var capacityOutcome = await capacityGuard.EnforceAsync(arm, armNames[arm.Id], pupil.Id, capacity, cancellationToken).ConfigureAwait(false);
        if (capacityOutcome.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(capacityOutcome.Error);
        }

        var tracked = await enrolments.FindTrackedByIdAsync(latest.Id, cancellationToken).ConfigureAwait(false);
        var reopen = tracked!.Reopen();
        if (reopen.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(reopen.Error);
        }

        var fromStatus = pupil.Status;
        var reactivate = pupil.Reactivate();
        if (reactivate.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(reactivate.Error);
        }

        var note = UndoPupilStatusChangeCommandValidator.NotePrefix +
            (string.IsNullOrWhiteSpace(request.Reason) ? "recorded in error." : request.Reason.Trim());
        var recorded = await engine
            .RecordStatusChangeAsync(pupil, fromStatus, last.EffectiveDate, note, null, arm.Id, undo: true, cancellationToken)
            .ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(recorded.Error);
        }

        var affected = await engine.AssessAsync([arm], armNames, last.EffectiveDate, publishedBlocks: false, cancellationToken).ConfigureAwait(false);
        await engine.ApplyAsync(affected, PupilMovementEngine.CohortNote("an undone status change", last.EffectiveDate), cancellationToken)
            .ConfigureAwait(false);

        return PupilMovementEngine.Outcome(
            false, pupil, fromStatus, PupilStatus.Active, null, arm, armNames, last.EffectiveDate, null, affected, capacity, today);
    }

    /// <summary>Not undoable. The caller's privilege is still checked first, so a refusal never reveals more than a 403 would.</summary>
    private async Task<Result<PupilMovementOutcomeDto>> RefuseAsync(Guid pupilId, CancellationToken cancellationToken)
    {
        var allowed = await access.CheckAsync(pupilId, Privileges.Pupil.StatusUpdate, cancellationToken).ConfigureAwait(false);
        return allowed.IsFailure ? Result.Failure<PupilMovementOutcomeDto>(allowed.Error) : Unavailable();
    }

    private static Result<PupilMovementOutcomeDto> Unavailable() =>
        Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
            "pupil.status_undo_unavailable",
            "Only a leaving status change recorded today can be undone. Reactivate the pupil from the status screen instead."));
}
