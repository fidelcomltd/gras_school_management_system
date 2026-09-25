using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Classes;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Movement;

/// <summary>
/// <c>POST /api/v1/pupils/{id}/transfer</c> (spec 6.5.17, 06 §6.4.4): moves an active pupil to another arm in the same
/// session. The open enrolment closes the day before <paramref name="EffectiveDate"/> and a new one opens on it. Marks
/// stay with the pupil. Every non-Published result set of both arms is flagged for recompute (Awaiting Approval and
/// Returned for Correction drop to Draft); a Published set of either arm for the term the date falls in, or a later
/// one, refuses the move.
/// </summary>
/// <param name="Id">The pupil. From the route.</param>
/// <param name="ArmId">The destination: an active arm in the same session as the pupil's current arm.</param>
/// <param name="EffectiveDate">The pupil's first day in the destination. After their current enrolment began, and not after today.</param>
/// <param name="DryRun">When true, nothing is written and the response lists the publication and recompute consequences.</param>
public sealed record TransferPupilCommand(Guid Id, string ArmId, DateOnly EffectiveDate, bool DryRun = false)
    : ICommand<Result<PupilMovementOutcomeDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class TransferPupilCommandValidator : AbstractValidator<TransferPupilCommand>
{
    public TransferPupilCommandValidator() =>
        RuleFor(command => command.ArmId)
            .NotEmpty()
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
}

/// <summary>Handles <see cref="TransferPupilCommand"/>. <c>pupil.transfer</c> is checked in the handler, as for the status screen.</summary>
internal sealed class TransferPupilHandler(
    PupilRecordAccess access,
    IPupilRepository pupils,
    IEnrolmentRepository enrolments,
    IArmRepository arms,
    PupilMovementEngine engine,
    ArmCapacityGuard capacityGuard,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<TransferPupilCommand, Result<PupilMovementOutcomeDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilMovementOutcomeDto>> HandleAsync(TransferPupilCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allowed = await access.CheckAsync(request.Id, Privileges.Pupil.Transfer, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(allowed.Error);
        }

        var pupil = await pupils.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (pupil is null)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        var open = pupil.Status == PupilStatus.Active
            ? await enrolments.FindOpenTrackedByPupilIdAsync(pupil.Id, cancellationToken).ConfigureAwait(false)
            : null;
        if (open is null)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
                "pupil.not_in_a_class",
                "Only an active pupil enrolled in a class can be moved. To bring back a pupil who left, reactivate them from the status screen."));
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var future = PupilMovementEngine.RefuseIfFuture(request.EffectiveDate, today);
        if (future.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(future.Error);
        }

        var source = await arms.FindReadOnlyByIdAsync(open.ArmId, cancellationToken).ConfigureAwait(false);
        var destination = await arms.FindReadOnlyByIdAsync(Guid.Parse(request.ArmId), cancellationToken).ConfigureAwait(false);

        if (source is null || source.Status == ArmStatus.Closed)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Conflict(
                "pupil.source_arm_closed", "The pupil's class belongs to a closed session and can no longer change."));
        }

        if (destination is null || destination.Status != ArmStatus.Active || destination.SessionId != source.SessionId)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "pupil.arm_not_available",
                "Choose an active class in the pupil's current session. Moving a pupil into another session is promotion, not a transfer."));
        }

        if (destination.Id == source.Id)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "enrolment.transfer_destination_is_current_arm", "The pupil is already enrolled in this class."));
        }

        if (request.EffectiveDate <= open.EffectiveFrom)
        {
            return Result.Failure<PupilMovementOutcomeDto>(Error.Validation(
                "enrolment.transfer_date_not_after_start",
                $"A move must take effect after {open.EffectiveFrom:dd/MM/yyyy}, the date the pupil joined this class."));
        }

        var armNames = await engine.DisplayNamesAsync([source, destination], cancellationToken).ConfigureAwait(false);
        var capacity = await capacityGuard.CheckAsync(destination, cancellationToken).ConfigureAwait(false);
        var affected = await engine
            .AssessAsync([source, destination], armNames, request.EffectiveDate, publishedBlocks: true, cancellationToken)
            .ConfigureAwait(false);
        var closesOn = request.EffectiveDate.AddDays(-1);

        if (request.DryRun)
        {
            return PupilMovementEngine.Outcome(
                true, pupil, pupil.Status, pupil.Status, source, destination, armNames, request.EffectiveDate, closesOn, affected, capacity, today);
        }

        var blocked = PupilMovementEngine.RefuseIfBlocked(affected, source.Id);
        if (blocked.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(blocked.Error);
        }

        var capacityOutcome = await capacityGuard
            .EnforceAsync(destination, armNames[destination.Id], pupil.Id, capacity, cancellationToken)
            .ConfigureAwait(false);
        if (capacityOutcome.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(capacityOutcome.Error);
        }

        var moved = Enrolment.Transfer(open, Guid.CreateVersion7(), destination.Id, request.EffectiveDate);
        if (moved.IsFailure)
        {
            return Result.Failure<PupilMovementOutcomeDto>(moved.Error);
        }

        await enrolments.AddAsync(moved.Value, cancellationToken).ConfigureAwait(false);

        await engine.ApplyAsync(affected, PupilMovementEngine.CohortNote("pupil transfer", request.EffectiveDate), cancellationToken)
            .ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Pupil.Transfer,
            "pupil",
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["armId"] = destination.Id.ToString("D", CultureInfo.InvariantCulture),
                ["effectiveDate"] = request.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["armId"] = source.Id.ToString("D", CultureInfo.InvariantCulture),
            }).ConfigureAwait(false);

        return PupilMovementEngine.Outcome(
            false, pupil, pupil.Status, pupil.Status, source, destination, armNames, request.EffectiveDate, closesOn, affected, capacity, today);
    }
}
