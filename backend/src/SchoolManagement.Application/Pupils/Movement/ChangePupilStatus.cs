using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Classes;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Movement;

/// <summary>
/// <c>POST /api/v1/pupils/{id}/status</c> (spec 6.5.14, 6.5.17): the status screen. Active to transferred, withdrawn or
/// graduated closes the open enrolment on the effective date (a manual graduation included, human ruling 2026-09-25, so
/// a mistaken one can be reversed; promotion at the terminal level will close at the session end itself); transferred,
/// withdrawn or graduated back to active (reactivation) opens a new enrolment in <paramref name="ArmId"/>, keeping the
/// registration number and all history. A pending admission is approved or declined from the admissions queue instead.
/// </summary>
/// <param name="Id">The pupil. From the route.</param>
/// <param name="TargetStatus">Active, transferred, withdrawn or graduated.</param>
/// <param name="EffectiveDate">Required, not after today.</param>
/// <param name="Reason">Required when the pupil leaves, and when a graduated pupil is reactivated. At most 500 characters.</param>
/// <param name="ArmId">The destination arm for a reactivation: an active arm in the active session. Omitted otherwise.</param>
/// <param name="DryRun">When true, nothing is written and the response says what would happen.</param>
public sealed record ChangePupilStatusCommand(
    Guid Id,
    PupilStatus TargetStatus,
    DateOnly? EffectiveDate,
    string? Reason,
    string? ArmId,
    bool DryRun = false)
    : ICommand<Result<PupilMovementOutcomeDto>>;

/// <summary>Structural checks; whether the transition is allowed from the pupil's current status lives in the handler.</summary>
internal sealed class ChangePupilStatusCommandValidator : AbstractValidator<ChangePupilStatusCommand>
{
    public ChangePupilStatusCommandValidator()
    {
        RuleFor(command => command.TargetStatus)
            .IsInEnum()
            .NotEqual(PupilStatus.Pending)
            .WithMessage("A pupil cannot be set back to pending.");

        RuleFor(command => command.EffectiveDate)
            .NotNull()
            .WithMessage("Enter the date the change takes effect.");

        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithMessage("Enter the reason for this change.")
            .When(command => command.TargetStatus is PupilStatus.Transferred or PupilStatus.Withdrawn or PupilStatus.Graduated);

        RuleFor(command => command.Reason)
            .MaximumLength(PupilStatusChange.ReasonMaxLength)
            .When(command => command.Reason is not null);

        RuleFor(command => command.ArmId)
            .NotEmpty()
            .WithMessage("Choose the class the pupil returns to.")
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.")
            .When(command => command.TargetStatus == PupilStatus.Active);

        RuleFor(command => command.ArmId)
            .Empty()
            .WithMessage("A class is chosen only when reactivating a pupil.")
            .When(command => command.TargetStatus != PupilStatus.Active);
    }
}

/// <summary>Handles <see cref="ChangePupilStatusCommand"/>.</summary>
/// <remarks>
/// <c>pupil.status.update</c> is checked here, not at the route: a leaver has no open enrolment, so a route-level pupil
/// scope could not resolve them (<see cref="PupilRecordAccess"/>).
/// </remarks>
internal sealed class ChangePupilStatusHandler(
    PupilRecordAccess access,
    IPupilRepository pupils,
    IEnrolmentRepository enrolments,
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    PupilMovementEngine engine,
    ArmCapacityGuard capacityGuard,
    TimeProvider timeProvider)
    : IRequestHandler<ChangePupilStatusCommand, Result<PupilMovementOutcomeDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilMovementOutcomeDto>> HandleAsync(ChangePupilStatusCommand request, CancellationToken cancellationToken)
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

        if (pupil.Status == PupilStatus.Pending)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
                "pupil.status_pending", "This pupil is a pending admission. Approve or decline it from the admissions queue."));
        }

        if (pupil.Status == request.TargetStatus)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
                "pupil.status_unchanged", $"This pupil is already {request.TargetStatus.ToString().ToLowerInvariant()}."));
        }

        var today = Weekly.WeeklyProjection.LagosToday(timeProvider.GetUtcNow());

        return request.TargetStatus == PupilStatus.Active
            ? await ReactivateAsync(request, pupil, today, cancellationToken).ConfigureAwait(false)
            : await LeaveAsync(request, pupil, today, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<PupilMovementOutcomeDto>> LeaveAsync(
        ChangePupilStatusCommand request, Pupil pupil, DateOnly today, CancellationToken cancellationToken)
    {
        var check = pupil.CheckLeave(request.TargetStatus);
        if (check.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(check.Error);
        }

        var open = await enrolments.FindOpenTrackedByPupilIdAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var arm = open is null ? null : await arms.FindReadOnlyByIdAsync(open.ArmId, cancellationToken).ConfigureAwait(false);
        var armNames = await engine.DisplayNamesAsync(arm is null ? [] : [arm], cancellationToken).ConfigureAwait(false);

        var effectiveDate = request.EffectiveDate!.Value;
        var future = PupilMovementEngine.RefuseIfFuture(effectiveDate, today);
        if (future.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(future.Error);
        }

        if (open is not null && effectiveDate < open.EffectiveFrom)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "pupil.effective_date_before_enrolment",
                $"The effective date cannot be before {open.EffectiveFrom:dd/MM/yyyy}, when the pupil joined {armNames[open.ArmId]}."));
        }

        var affected = arm is null
            ? []
            : await engine.AssessAsync([arm], armNames, effectiveDate, publishedBlocks: false, cancellationToken).ConfigureAwait(false);

        var fromStatus = pupil.Status;

        if (request.DryRun)
        {
            return PupilMovementEngine.Outcome(true, pupil, fromStatus, request.TargetStatus, arm, null, armNames, effectiveDate, open is null ? null : effectiveDate, affected, null, today);
        }

        if (open is not null)
        {
            var close = open.Close(effectiveDate);
            if (close.IsFailure)
            {
                return Result.Failure<PupilMovementOutcomeDto>(close.Error);
            }
        }

        var leave = pupil.Leave(request.TargetStatus);
        if (leave.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(leave.Error);
        }

        var recorded = await engine
            .RecordStatusChangeAsync(pupil, fromStatus, effectiveDate, request.Reason, arm?.Id, null, undo: false, cancellationToken)
            .ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(recorded.Error);
        }

        await engine.ApplyAsync(affected, PupilMovementEngine.CohortNote("a pupil leaving", effectiveDate), cancellationToken)
            .ConfigureAwait(false);

        return PupilMovementEngine.Outcome(false, pupil, fromStatus, request.TargetStatus, arm, null, armNames, effectiveDate, open is null ? null : effectiveDate, affected, null, today);
    }

    private async Task<Result<PupilMovementOutcomeDto>> ReactivateAsync(
        ChangePupilStatusCommand request, Pupil pupil, DateOnly today, CancellationToken cancellationToken)
    {
        var check = pupil.CheckReactivation();
        if (check.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(check.Error);
        }

        if (pupil.Status == PupilStatus.Graduated && string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "pupil.reason_required", "Enter the reason for reactivating a graduated pupil."));
        }

        var effectiveDate = request.EffectiveDate!.Value;
        var future = PupilMovementEngine.RefuseIfFuture(effectiveDate, today);
        if (future.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(future.Error);
        }

        var activeSession = await sessions.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        var arm = await arms.FindReadOnlyByIdAsync(Guid.Parse(request.ArmId!), cancellationToken).ConfigureAwait(false);

        if (activeSession is null || arm is null || arm.SessionId != activeSession.Id || arm.Status != ArmStatus.Active)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "pupil.arm_not_available", "Choose an active class in the current session."));
        }

        var armAllowed = await access.CheckArmAsync(arm.Id, Privileges.Pupil.StatusUpdate, cancellationToken).ConfigureAwait(false);
        if (armAllowed.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(armAllowed.Error);
        }

        if (effectiveDate < activeSession.StartDate)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "pupil.effective_date_before_session",
                $"The effective date cannot be before {activeSession.StartDate:dd/MM/yyyy}, when the current session started."));
        }

        var history = await enrolments.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);

        if (history.Any(enrolment => enrolment.IsOpen))
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
                "pupil.enrolment_still_open", "This pupil still has an open enrolment. Close it before reactivating."));
        }

        // No date may belong to two enrolments.
        var lastDay = history.Max(enrolment => enrolment.EffectiveTo);
        if (lastDay is { } last && effectiveDate <= last)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "pupil.effective_date_overlaps_enrolment",
                $"The pupil's last enrolment ran to {last:dd/MM/yyyy}. Reactivate from {last.AddDays(1):dd/MM/yyyy} or later."));
        }

        var armNames = await engine.DisplayNamesAsync([arm], cancellationToken).ConfigureAwait(false);
        var capacity = await capacityGuard.CheckAsync(arm, cancellationToken).ConfigureAwait(false);
        var affected = await engine.AssessAsync([arm], armNames, effectiveDate, publishedBlocks: false, cancellationToken).ConfigureAwait(false);
        var fromStatus = pupil.Status;

        if (request.DryRun)
        {
            return PupilMovementEngine.Outcome(true, pupil, fromStatus, PupilStatus.Active, null, arm, armNames, effectiveDate, null, affected, capacity, today);
        }

        var capacityOutcome = await capacityGuard.EnforceAsync(arm, armNames[arm.Id], pupil.Id, capacity, cancellationToken).ConfigureAwait(false);
        if (capacityOutcome.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(capacityOutcome.Error);
        }

        var enrolment = Enrolment.Open(Guid.CreateVersion7(), pupil.Id, arm.Id, effectiveDate);
        if (enrolment.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(enrolment.Error);
        }

        await enrolments.AddAsync(enrolment.Value, cancellationToken).ConfigureAwait(false);

        var reactivate = pupil.Reactivate();
        if (reactivate.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(reactivate.Error);
        }

        var recorded = await engine
            .RecordStatusChangeAsync(pupil, fromStatus, effectiveDate, request.Reason, null, arm.Id, undo: false, cancellationToken)
            .ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(recorded.Error);
        }

        await engine.ApplyAsync(affected, PupilMovementEngine.CohortNote("a pupil returning", effectiveDate), cancellationToken)
            .ConfigureAwait(false);

        return PupilMovementEngine.Outcome(false, pupil, fromStatus, PupilStatus.Active, null, arm, armNames, effectiveDate, null, affected, capacity, today);
    }
}
