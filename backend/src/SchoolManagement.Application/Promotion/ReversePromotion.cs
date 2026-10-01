using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Promotion;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Pupils.Movement;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Promotion;

/// <summary>
/// <c>POST /api/v1/promotion-batches/{batchId}/reverse</c> (spec 6.3.7): undoes a committed promotion, while no mark has been
/// entered and no pin used in the new session. Needs <c>promotion.reverse</c>, a Super Admin, and a reason. The batch row is
/// kept, marked.
/// </summary>
/// <param name="BatchId">From the route.</param>
/// <param name="Reason">10 to 500 characters.</param>
public sealed record ReversePromotionCommand(Guid BatchId, string Reason) : ICommand<Result<PromotionBatchDto>>;

/// <summary>Reason bounds.</summary>
internal sealed class ReversePromotionCommandValidator : AbstractValidator<ReversePromotionCommand>
{
    /// <summary>Configures the rules.</summary>
    public ReversePromotionCommandValidator()
    {
        RuleFor(command => command.Reason)
            .Must(reason => reason?.Trim().Length is >= PromotionBatch.ReasonMinLength and <= PromotionBatch.ReasonMaxLength)
            .WithMessage($"Give a reason of {PromotionBatch.ReasonMinLength} to {PromotionBatch.ReasonMaxLength} characters.");
    }
}

/// <summary>Handles <see cref="ReversePromotionCommand"/>.</summary>
internal sealed class ReversePromotionHandler(
    IPromotionRepository promotions,
    IAcademicSessionRepository sessions,
    IAdminAccountRepository accounts,
    PupilMovementEngine movement,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<ReversePromotionCommand, Result<PromotionBatchDto>>
{
    private const string MoveInstead = "This promotion cannot be reversed. Move individual pupils between arms instead.";

    /// <inheritdoc />
    public async Task<Result<PromotionBatchDto>> HandleAsync(ReversePromotionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Spec 6.3.7: promotion.reverse is held only by a Super Admin; checked here too, so a custom role granted it cannot.
        var actor = currentUser.UserId is { } userId && Guid.TryParse(userId, out var actorId)
            ? await accounts.FindReadOnlyByIdAsync(actorId, cancellationToken).ConfigureAwait(false)
            : null;
        if (actor is not { IsSuperAdmin: true })
        {
            return Result.Failure<PromotionBatchDto>(Error.Forbidden(
                "promotion.reverse_requires_super_admin", "Only a Super Admin may reverse a promotion."));
        }

        var batch = await promotions.FindTrackedWithDecisionsAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PromotionBatchDto>(Error.NotFound("promotion.batch_not_found", "No promotion batch was found with that id."));
        }

        // The same lock a commit takes, so a reversal and a re-run of this session never interleave.
        await promotions.LockSessionAsync(batch.SourceSessionId, cancellationToken).ConfigureAwait(false);
        if (batch.State == PromotionBatchState.Reversed)
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict("promotion.already_reversed", "This promotion has already been reversed."));
        }

        var target = await sessions.FindReadOnlyByIdAsync(batch.TargetSessionId, cancellationToken).ConfigureAwait(false);
        var targetName = target?.Name ?? "the new session";
        if (await promotions.AnyMarkInSessionAsync(batch.TargetSessionId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict("promotion.marks_entered", $"Marks have already been entered in {targetName}. {MoveInstead}"));
        }

        var pupilIds = batch.Decisions.Select(decision => decision.PupilId).ToList();
        if (await promotions.AnyPinUseInSessionAsync(batch.TargetSessionId, pupilIds, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict(
                "promotion.pin_used", $"A result pin has already been used against a pupil in {targetName}. {MoveInstead}"));
        }

        var openByPupil = (await promotions.ListOpenEnrolmentsTrackedAsync(pupilIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(enrolment => enrolment.PupilId);
        var pupils = (await promotions.ListPupilsTrackedAsync(pupilIds, cancellationToken).ConfigureAwait(false)).ToDictionary(pupil => pupil.Id);
        var closed = (await promotions.ListEnrolmentsTrackedAsync([.. batch.Decisions.Select(decision => decision.ClosedEnrolmentId)], cancellationToken)
            .ConfigureAwait(false)).ToDictionary(enrolment => enrolment.Id);

        // Reversal puts back exactly what the batch did, so every pupil must still be where the batch left them.
        var moved = batch.Decisions.Count(decision =>
        {
            var open = openByPupil.GetValueOrDefault(decision.PupilId);
            var status = pupils.GetValueOrDefault(decision.PupilId)?.Status;
            var inPlace = decision.NewEnrolmentId is { } newId
                ? open?.Id == newId && status == PupilStatus.Active
                : open is null && status == PupilStatus.Graduated;
            return !inPlace || !closed.ContainsKey(decision.ClosedEnrolmentId);
        });
        if (moved > 0)
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict(
                "promotion.pupils_moved", $"{moved} {(moved == 1 ? "pupil has" : "pupils have")} moved or changed status since this promotion. {MoveInstead}"));
        }

        var today = SchoolTime.Today(timeProvider.GetUtcNow());
        foreach (var decision in batch.Decisions)
        {
            if (decision.NewEnrolmentId is { } newId)
            {
                // Deleted at once, so the reopened enrolment never meets it under the one-open-enrolment index.
                await promotions.RemoveEnrolmentAsync(newId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var pupil = pupils[decision.PupilId];
                var reactivated = pupil.Reactivate();
                if (reactivated.IsFailure)
                {
                    return Result.Failure<PromotionBatchDto>(reactivated.Error);
                }

                var recorded = await movement.RecordStatusChangeAsync(
                    pupil, PupilStatus.Graduated, today, "Promotion reversed.", null, decision.FromArmId, undo: false, cancellationToken).ConfigureAwait(false);
                if (recorded.IsFailure)
                {
                    return Result.Failure<PromotionBatchDto>(recorded.Error);
                }
            }

            var reopened = closed[decision.ClosedEnrolmentId].Reopen();
            if (reopened.IsFailure)
            {
                return Result.Failure<PromotionBatchDto>(reopened.Error);
            }
        }

        var reversed = batch.Reverse(request.Reason, timeProvider.GetUtcNow(), currentUser.UserId);
        if (reversed.IsFailure)
        {
            return Result.Failure<PromotionBatchDto>(reversed.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Promotion.Reverse,
            "promotion_batch",
            batch.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourceSessionId"] = batch.SourceSessionId.ToString("D", CultureInfo.InvariantCulture),
                ["targetSessionId"] = batch.TargetSessionId.ToString("D", CultureInfo.InvariantCulture),
                ["pupils"] = batch.Decisions.Count,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            reason: batch.ReversalReason).ConfigureAwait(false);

        return Result.Success(PromotionBatchDto.From(batch, targetName));
    }
}
