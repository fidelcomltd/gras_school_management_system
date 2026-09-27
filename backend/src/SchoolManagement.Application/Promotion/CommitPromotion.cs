using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Promotion;
using SchoolManagement.Application.Pupils.Movement;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Promotion;

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

        // Stop at the first failure: the later rules read the list, and a missing list or a null row must be a 422, never a 500.
        RuleFor(command => command.Decisions)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(decisions => decisions.All(decision => decision is not null)).WithMessage("Every decision must be filled in.")
            .Must(decisions => decisions.Select(decision => decision.PupilId).Distinct().Count() == decisions.Count)
            .WithMessage("Each pupil can appear only once.");
        RuleForEach(command => command.Decisions)
            .ChildRules(decision =>
            {
                decision.RuleFor(input => input.PupilId).NotEmpty();
                decision.RuleFor(input => input.Outcome).IsInEnum();
                decision.RuleFor(input => input.Reason).MaximumLength(PromotionBatch.ReasonMaxLength);
            })
            .When(command => command.Decisions is not null && command.Decisions.All(decision => decision is not null));
    }
}

/// <summary>Handles <see cref="CommitPromotionCommand"/>.</summary>
internal sealed class CommitPromotionHandler(
    PromotionPlanner planner,
    IPromotionRepository promotions,
    IEnrolmentRepository enrolments,
    PupilMovementEngine movement,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CommitPromotionCommand, Result<PromotionBatchDto>>
{
    private const string GraduationNote = "Graduated through end-of-session promotion.";

    /// <inheritdoc />
    public async Task<Result<PromotionBatchDto>> HandleAsync(CommitPromotionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Serialises concurrent commits for this session: the second waits, then its plan sees the first's batch (409).
        await promotions.LockSessionAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
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
        var submitted = request.Decisions.Select(decision => decision.PupilId).ToHashSet();
        var missing = rows.Keys.Count(id => !submitted.Contains(id));
        var unexpected = submitted.Count(id => !rows.ContainsKey(id));
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

        var openByPupil = (await promotions.ListOpenEnrolmentsTrackedAsync(submitted, cancellationToken).ConfigureAwait(false))
            .ToDictionary(enrolment => enrolment.PupilId);
        var graduateIds = request.Decisions.Where(decision => decision.Outcome == PromotionDecisionOutcome.Graduated).Select(decision => decision.PupilId).ToList();
        var graduates = graduateIds.Count == 0
            ? []
            : (await promotions.ListPupilsTrackedAsync(graduateIds, cancellationToken).ConfigureAwait(false)).ToDictionary(pupil => pupil.Id);

        var batch = PromotionBatch.Commit(Guid.CreateVersion7(), plan.Source.Id, target.Id, timeProvider.GetUtcNow(), currentUser.UserId);
        foreach (var decision in request.Decisions)
        {
            var row = rows[decision.PupilId];
            if (!openByPupil.TryGetValue(decision.PupilId, out var open) || open.ArmId != row.CurrentArmId)
            {
                return Result.Failure<PromotionBatchDto>(Error.Conflict(
                    "promotion.pupils_changed", $"{row.DisplayName} has moved since this list was loaded. Reload it and review again."));
            }

            // The session's end date, unless the enrolment itself started later (a late admission): never before its start.
            var closesOn = open.EffectiveFrom > plan.Source.EndDate ? open.EffectiveFrom : plan.Source.EndDate;
            var closed = open.Close(closesOn);
            if (closed.IsFailure)
            {
                return Result.Failure<PromotionBatchDto>(closed.Error);
            }

            Guid? newEnrolmentId = null;
            if (decision.Outcome == PromotionDecisionOutcome.Graduated)
            {
                var graduated = await GraduateAsync(graduates.GetValueOrDefault(decision.PupilId), closesOn, row.CurrentArmId, cancellationToken).ConfigureAwait(false);
                if (graduated.IsFailure)
                {
                    return Result.Failure<PromotionBatchDto>(graduated.Error);
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

    // A graduation is a status change like any other: its row keeps the pupil's movement history and same-day undo honest.
    private async Task<Result> GraduateAsync(Pupil? pupil, DateOnly effectiveDate, Guid fromArmId, CancellationToken cancellationToken)
    {
        if (pupil is null)
        {
            return Result.Failure(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        var left = pupil.Leave(PupilStatus.Graduated);
        return left.IsFailure
            ? left
            : await movement.RecordStatusChangeAsync(pupil, PupilStatus.Active, effectiveDate, GraduationNote, fromArmId, null, undo: false, cancellationToken)
                .ConfigureAwait(false);
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
