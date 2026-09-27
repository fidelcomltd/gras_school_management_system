using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Promotion;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Promotion;

/// <summary>
/// <c>GET /api/v1/sessions/{sessionId}/promotion/preview</c> (spec 6.3.7): the review screen's rows, destination arms and
/// blockers. Needs <c>promotion.run</c>.
/// </summary>
/// <param name="SessionId">The session promoted from.</param>
/// <param name="TargetSessionId">The session promoted into; the next session by start date when absent.</param>
public sealed record GetPromotionPreviewQuery(Guid SessionId, Guid? TargetSessionId) : IQuery<Result<PromotionPreviewDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPromotionPreviewQueryValidator : AbstractValidator<GetPromotionPreviewQuery>;

/// <summary>Handles <see cref="GetPromotionPreviewQuery"/>.</summary>
internal sealed class GetPromotionPreviewHandler(PromotionPlanner planner) : IRequestHandler<GetPromotionPreviewQuery, Result<PromotionPreviewDto>>
{
    /// <inheritdoc />
    public async Task<Result<PromotionPreviewDto>> HandleAsync(GetPromotionPreviewQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var planned = await planner.PlanAsync(request.SessionId, request.TargetSessionId, cancellationToken).ConfigureAwait(false);
        if (planned.IsFailure)
        {
            return Result.Failure<PromotionPreviewDto>(planned.Error);
        }

        var plan = planned.Value;
        return Result.Success(new PromotionPreviewDto(
            Session(plan.Source),
            plan.Target is null ? null : Session(plan.Target),
            plan.Blockers,
            plan.Rows,
            plan.TargetArms,
            plan.CoreSubjects,
            [.. plan.Excluded.Select(pupil => new PromotionExcludedPupilDto(pupil.Id, PromotionPlanner.DisplayName(pupil), pupil.RegistrationNumber, pupil.Status))],
            plan.CommittedBatch is null ? null : PromotionBatchDto.From(plan.CommittedBatch, plan.Target?.Name ?? string.Empty),
            plan.CanDecide));
    }

    private static PromotionSessionDto Session(Domain.Sessions.AcademicSession session) =>
        new(session.Id, session.Name, session.StartDate, session.EndDate);
}

/// <summary>One pupil's decision as the review screen submits it.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Outcome">Promoted, Repeat, PromotedOnTrial or Graduated.</param>
/// <param name="TargetArmId">An arm of the destination level in the target session; null for a graduate.</param>
/// <param name="Reason">Required for PromotedOnTrial; optional otherwise.</param>
public sealed record PromotionDecisionInput(Guid PupilId, PromotionDecisionOutcome Outcome, Guid? TargetArmId, string? Reason);

/// <summary>
/// <c>POST /api/v1/sessions/{sessionId}/promotion</c> (spec 6.3.7): commits the whole school's promotion in one transaction.
/// Every active pupil appears exactly once. Needs <c>promotion.run</c>; changing a proposal or choosing on trial also needs
/// <c>promotion.decide</c>.
/// </summary>
/// <param name="SessionId">From the route.</param>
/// <param name="TargetSessionId">The session promoted into, as the preview named it.</param>
/// <param name="Decisions">One per active pupil.</param>
public sealed record CommitPromotionCommand(Guid SessionId, Guid TargetSessionId, IReadOnlyList<PromotionDecisionInput> Decisions)
    : ICommand<Result<PromotionBatchDto>>;

/// <summary>Shape rules; the rules that read the plan live in the handler.</summary>
internal sealed class CommitPromotionCommandValidator : AbstractValidator<CommitPromotionCommand>
{
    /// <summary>Configures the rules.</summary>
    public CommitPromotionCommandValidator()
    {
        RuleFor(command => command.TargetSessionId).NotEmpty();
        RuleFor(command => command.Decisions).NotEmpty()
            .Must(decisions => decisions.Select(decision => decision.PupilId).Distinct().Count() == decisions.Count)
            .WithMessage("Each pupil can appear only once.");
        RuleForEach(command => command.Decisions).ChildRules(decision =>
        {
            decision.RuleFor(input => input.PupilId).NotEmpty();
            decision.RuleFor(input => input.Outcome).IsInEnum();
            decision.RuleFor(input => input.Reason).MaximumLength(PromotionBatch.ReasonMaxLength);
        });
    }
}

/// <summary>Handles <see cref="CommitPromotionCommand"/>.</summary>
internal sealed class CommitPromotionHandler(
    PromotionPlanner planner,
    IPromotionRepository promotions,
    IEnrolmentRepository enrolments,
    IPupilRepository pupils,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CommitPromotionCommand, Result<PromotionBatchDto>>
{
    /// <inheritdoc />
    public async Task<Result<PromotionBatchDto>> HandleAsync(CommitPromotionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var planned = await planner.PlanAsync(request.SessionId, request.TargetSessionId, cancellationToken).ConfigureAwait(false);
        if (planned.IsFailure)
        {
            return Result.Failure<PromotionBatchDto>(planned.Error);
        }

        var plan = planned.Value;
        if (plan.Blockers.Count > 0)
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict(plan.Blockers[0].Code, string.Join(' ', plan.Blockers.Select(blocker => blocker.Message))));
        }

        var target = plan.Target!;
        var rows = plan.Rows.ToDictionary(row => row.PupilId);
        var missing = rows.Keys.Count(id => request.Decisions.All(decision => decision.PupilId != id));
        var unexpected = request.Decisions.Count(decision => !rows.ContainsKey(decision.PupilId));
        if (missing > 0 || unexpected > 0)
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict(
                "promotion.pupils_changed",
                $"The pupils to promote have changed since this list was loaded ({missing} missing, {unexpected} not expected). Reload it and review again."));
        }

        var targetArms = plan.TargetArms.ToDictionary(arm => arm.ArmId);
        foreach (var decision in request.Decisions)
        {
            var checkedDecision = Check(decision, rows[decision.PupilId], targetArms, plan.CanDecide, target.Name);
            if (checkedDecision.IsFailure)
            {
                return Result.Failure<PromotionBatchDto>(checkedDecision.Error);
            }
        }

        var batch = PromotionBatch.Commit(Guid.CreateVersion7(), plan.Source.Id, target.Id, timeProvider.GetUtcNow(), currentUser.UserId);
        foreach (var decision in request.Decisions)
        {
            var row = rows[decision.PupilId];
            var open = await enrolments.FindOpenTrackedByPupilIdAsync(decision.PupilId, cancellationToken).ConfigureAwait(false);
            if (open is null || open.ArmId != row.CurrentArmId)
            {
                return Result.Failure<PromotionBatchDto>(Error.Conflict(
                    "promotion.pupils_changed", $"{row.DisplayName} has moved since this list was loaded. Reload it and review again."));
            }

            var closed = open.Close(plan.Source.EndDate);
            if (closed.IsFailure)
            {
                return Result.Failure<PromotionBatchDto>(closed.Error);
            }

            Guid? newEnrolmentId = null;
            if (decision.Outcome == PromotionDecisionOutcome.Graduated)
            {
                var pupil = await pupils.FindTrackedByIdAsync(decision.PupilId, cancellationToken).ConfigureAwait(false);
                var left = pupil is null ? Result.Failure(Error.NotFound("pupil.not_found", "No pupil was found with that id.")) : pupil.Leave(PupilStatus.Graduated);
                if (left.IsFailure)
                {
                    return Result.Failure<PromotionBatchDto>(left.Error);
                }
            }
            else
            {
                var opened = Enrolment.Open(Guid.CreateVersion7(), decision.PupilId, decision.TargetArmId!.Value, target.StartDate);
                if (opened.IsFailure)
                {
                    return Result.Failure<PromotionBatchDto>(opened.Error);
                }

                await enrolments.AddAsync(opened.Value, cancellationToken).ConfigureAwait(false);
                newEnrolmentId = opened.Value.Id;
            }

            batch.Add(PromotionDecision.Create(
                Guid.CreateVersion7(), batch.Id, decision.PupilId, row.CurrentArmId, row.ProposedOutcome, decision.Outcome,
                decision.TargetArmId, string.IsNullOrWhiteSpace(decision.Reason) ? null : decision.Reason.Trim(), open.Id, newEnrolmentId));
        }

        await promotions.AddAsync(batch, cancellationToken).ConfigureAwait(false);
        var dto = PromotionBatchDto.From(batch, target.Name);
        await auditSink.RecordAsync(
            Privileges.Promotion.Run,
            "promotion_batch",
            batch.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourceSessionId"] = plan.Source.Id.ToString("D", CultureInfo.InvariantCulture),
                ["targetSessionId"] = target.Id.ToString("D", CultureInfo.InvariantCulture),
                ["promoted"] = dto.Promoted,
                ["repeated"] = dto.Repeated,
                ["promotedOnTrial"] = dto.PromotedOnTrial,
                ["graduated"] = dto.Graduated,
                ["overridden"] = batch.Decisions.Count(decision => decision.ProposedOutcome is not null && decision.ProposedOutcome != decision.Outcome),
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(dto);
    }

    private static Result Check(
        PromotionDecisionInput decision, PromotionRowDto row, Dictionary<Guid, PromotionTargetArmDto> targetArms, bool canDecide, string targetName)
    {
        var terminal = row.NextLevelId is null;
        var allowed = terminal
            ? decision.Outcome is PromotionDecisionOutcome.Graduated or PromotionDecisionOutcome.Repeat
            : decision.Outcome is not PromotionDecisionOutcome.Graduated;
        if (!allowed)
        {
            return Result.Failure(Error.Validation(
                "promotion.outcome_not_allowed",
                terminal
                    ? $"{row.DisplayName} is in the final class: they can graduate or repeat."
                    : $"{row.DisplayName} is not in the final class, so they cannot graduate."));
        }

        if (decision.Outcome == PromotionDecisionOutcome.PromotedOnTrial)
        {
            if (!canDecide)
            {
                return Result.Failure(Error.Forbidden("promotion.decide_required", "Promoting on trial needs the promotion.decide privilege."));
            }

            var reason = decision.Reason?.Trim();
            if (reason is null || reason.Length < PromotionBatch.ReasonMinLength)
            {
                return Result.Failure(Error.Validation(
                    "promotion.reason_required",
                    $"Give a reason of {PromotionBatch.ReasonMinLength} to {PromotionBatch.ReasonMaxLength} characters for promoting {row.DisplayName} on trial."));
            }
        }
        else if (row.ProposedOutcome is { } proposed && proposed != decision.Outcome && !canDecide)
        {
            return Result.Failure(Error.Forbidden(
                "promotion.decide_required", $"Changing {row.DisplayName}'s proposed outcome needs the promotion.decide privilege."));
        }

        var destination = PromotionPlanner.DestinationLevel(decision.Outcome, row.ClassLevelId, row.NextLevelId);
        if (destination is null)
        {
            return decision.TargetArmId is null
                ? Result.Success()
                : Result.Failure(Error.Validation("promotion.target_arm_invalid", $"{row.DisplayName} is graduating, so they take no target arm."));
        }

        return decision.TargetArmId is { } armId && targetArms.TryGetValue(armId, out var arm) && arm.ClassLevelId == destination
            ? Result.Success()
            : Result.Failure(Error.Validation(
                "promotion.target_arm_invalid", $"Choose an arm in {targetName} of the class {row.DisplayName} is going to."));
    }
}

/// <summary>
/// <c>POST /api/v1/promotion-batches/{batchId}/reverse</c> (spec 6.3.7): undoes a committed promotion, while no mark has been
/// entered and no pin used in the new session. Needs <c>promotion.reverse</c> and a reason. The batch row is kept, marked.
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
    IEnrolmentRepository enrolments,
    IPupilRepository pupils,
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
        var batch = await promotions.FindTrackedWithDecisionsAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PromotionBatchDto>(Error.NotFound("promotion.batch_not_found", "No promotion batch was found with that id."));
        }

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

        // Reversal puts back exactly what the batch did, so every pupil must still be where the batch left them.
        var moved = 0;
        foreach (var decision in batch.Decisions)
        {
            var open = await enrolments.FindOpenReadOnlyByPupilIdAsync(decision.PupilId, cancellationToken).ConfigureAwait(false);
            var pupil = await pupils.FindReadOnlyByIdAsync(decision.PupilId, cancellationToken).ConfigureAwait(false);
            var inPlace = decision.NewEnrolmentId is { } newId
                ? open?.Id == newId && pupil?.Status == PupilStatus.Active
                : open is null && pupil?.Status == PupilStatus.Graduated;
            moved += inPlace ? 0 : 1;
        }

        if (moved > 0)
        {
            return Result.Failure<PromotionBatchDto>(Error.Conflict(
                "promotion.pupils_moved", $"{moved} {(moved == 1 ? "pupil has" : "pupils have")} moved or changed status since this promotion. {MoveInstead}"));
        }

        foreach (var decision in batch.Decisions)
        {
            if (decision.NewEnrolmentId is { } newId)
            {
                await promotions.RemoveEnrolmentAsync(newId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var pupil = await pupils.FindTrackedByIdAsync(decision.PupilId, cancellationToken).ConfigureAwait(false);
                var reactivated = pupil!.Reactivate();
                if (reactivated.IsFailure)
                {
                    return Result.Failure<PromotionBatchDto>(reactivated.Error);
                }
            }

            var closed = await enrolments.FindTrackedByIdAsync(decision.ClosedEnrolmentId, cancellationToken).ConfigureAwait(false);
            var reopened = closed is null
                ? Result.Failure(Error.Conflict("promotion.enrolment_missing", $"An enrolment this promotion closed no longer exists. {MoveInstead}"))
                : closed.Reopen();
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
