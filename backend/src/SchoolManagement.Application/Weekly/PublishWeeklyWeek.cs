using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Weekly;

namespace SchoolManagement.Application.Weekly;

/// <summary>The body of the publish and unpublish routes.</summary>
/// <param name="TermId">The term the week belongs to.</param>
public sealed record WeeklyPublicationRequest(string TermId);

/// <summary>
/// <c>POST /api/v1/arms/{armId}/weekly/{weekNumber}/publish</c> or <c>/unpublish</c> (spec 6.10.8): per arm per week, no
/// approval chain, no reason. Publishing a week with no notes at all is refused.
/// </summary>
/// <param name="ArmId">From the route.</param>
/// <param name="WeekNumber">From the route.</param>
/// <param name="TermId">The term.</param>
/// <param name="Publish">True to publish, false to unpublish. Set by the route.</param>
public sealed record SetWeeklyPublicationCommand(string ArmId, int WeekNumber, string TermId, bool Publish)
    : ICommand<Result<WeeklyWeekSummaryDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class SetWeeklyPublicationCommandValidator : AbstractValidator<SetWeeklyPublicationCommand>
{
    public SetWeeklyPublicationCommandValidator()
    {
        RuleFor(command => command.ArmId).Must(value => Guid.TryParse(value, out _)).WithMessage("ArmId must be a valid identifier.");
        RuleFor(command => command.TermId).Must(value => Guid.TryParse(value, out _)).WithMessage("TermId must be a valid identifier.");
        RuleFor(command => command.WeekNumber).InclusiveBetween(1, TermWeeks.MaxWeeks);
    }
}

/// <summary>Handles <see cref="SetWeeklyPublicationCommand"/>.</summary>
internal sealed class SetWeeklyPublicationHandler(
    IArmRepository arms,
    ITermRepository terms,
    IWeeklyReportRepository weekly,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<SetWeeklyPublicationCommand, Result<WeeklyWeekSummaryDto>>
{
    /// <summary>Spec 6.10.8's refusal.</summary>
    public const string NothingToPublishErrorCode = "weekly.nothing_to_publish";

    /// <summary>The audit action for an unpublish; a publish records <see cref="Privileges.Weekly.Publish"/>.</summary>
    public const string UnpublishAction = "weekly.unpublish";

    /// <inheritdoc />
    public async Task<Result<WeeklyWeekSummaryDto>> HandleAsync(SetWeeklyPublicationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        if (await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is not { } arm)
        {
            return Result.Failure<WeeklyWeekSummaryDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        if (await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false) is not { } term || term.SessionId != arm.SessionId)
        {
            return Result.Failure<WeeklyWeekSummaryDto>(Error.NotFound("term.not_found", "No term was found with that id in this arm's session."));
        }

        var reports = await weekly.ListArmWeekTrackedAsync(armId, termId, request.WeekNumber, cancellationToken).ConfigureAwait(false);
        var derived = TermWeeks.Derive(term.StartDate, term.EndDate).FirstOrDefault(week => week.Number == request.WeekNumber);
        if (derived is null && reports.Count == 0)
        {
            return Result.Failure<WeeklyWeekSummaryDto>(Error.NotFound("weekly.week_not_found", $"This term has no week {request.WeekNumber}."));
        }

        if (request.Publish && !reports.Any(report => report.HasContent))
        {
            return Result.Failure<WeeklyWeekSummaryDto>(Error.Conflict(
                NothingToPublishErrorCode,
                $"Nothing has been written for Week {request.WeekNumber} yet. Add at least one note before publishing."));
        }

        // Spec 6.10.8: auto-publish removes a step; it must not overrule a teacher who published or hid this week by hand.
        if (derived is not null && await weekly.FindSettingTrackedAsync(armId, cancellationToken).ConfigureAwait(false) is { AutoPublish: true } setting)
        {
            setting.MarkAutoPublished(derived.StartDate);
        }

        var now = timeProvider.GetUtcNow();
        foreach (var report in reports)
        {
            if (request.Publish)
            {
                report.Publish(now, currentUser.UserId);
            }
            else
            {
                report.Unpublish();
            }
        }

        await auditSink.RecordAsync(
            request.Publish ? Privileges.Weekly.Publish : UnpublishAction,
            "weekly_report",
            entityId: null,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["armId"] = request.ArmId,
                ["termId"] = request.TermId,
                ["weekNumber"] = request.WeekNumber,
                ["reports"] = reports.Count,
            },
            currentUser.UserId,
            cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new WeeklyWeekSummaryDto(
            request.WeekNumber,
            derived?.StartDate ?? reports.Min(report => report.WeekStartDate),
            derived?.EndDate ?? reports.Min(report => report.WeekEndDate),
            derived is null || reports.Any(report => report.WeekStartDate != derived.StartDate),
            request.Publish,
            reports.Count(report => report.HasContent)));
    }
}

/// <summary><c>PUT /api/v1/arms/{armId}/weekly/settings</c> (spec 6.10.8): the per-arm auto-publish option.</summary>
/// <param name="ArmId">From the route.</param>
/// <param name="AutoPublish">Publish each week automatically at 17:00 on its Friday.</param>
public sealed record UpdateWeeklySettingsCommand(string ArmId, bool AutoPublish) : ICommand<Result<WeeklySettingsDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class UpdateWeeklySettingsCommandValidator : AbstractValidator<UpdateWeeklySettingsCommand>
{
    public UpdateWeeklySettingsCommandValidator() =>
        RuleFor(command => command.ArmId).Must(value => Guid.TryParse(value, out _)).WithMessage("ArmId must be a valid identifier.");
}

/// <summary>Handles <see cref="UpdateWeeklySettingsCommand"/>.</summary>
internal sealed class UpdateWeeklySettingsHandler(IArmRepository arms, IWeeklyReportRepository weekly, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<UpdateWeeklySettingsCommand, Result<WeeklySettingsDto>>
{
    /// <summary>The audit action.</summary>
    public const string Action = "weekly.settings.update";

    /// <inheritdoc />
    public async Task<Result<WeeklySettingsDto>> HandleAsync(UpdateWeeklySettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var armId = Guid.Parse(request.ArmId);
        if (await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<WeeklySettingsDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var setting = await weekly.FindSettingTrackedAsync(armId, cancellationToken).ConfigureAwait(false);
        var previous = setting?.AutoPublish ?? false;
        if (setting is null)
        {
            await weekly.AddSettingAsync(ArmWeeklySetting.Create(armId, request.AutoPublish), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            setting.SetAutoPublish(request.AutoPublish);
        }

        if (previous != request.AutoPublish)
        {
            await auditSink.RecordAsync(
                Action,
                "arm_weekly_setting",
                request.ArmId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["autoPublish"] = request.AutoPublish },
                currentUser.UserId,
                cancellationToken,
                beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["autoPublish"] = previous })
                .ConfigureAwait(false);
        }

        return Result.Success(new WeeklySettingsDto(WeeklyProjection.Id(armId), request.AutoPublish));
    }
}
