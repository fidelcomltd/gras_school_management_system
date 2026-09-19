using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Handles <see cref="CreateRemarkTemplateCommand"/>. PRIVILEGE IS DATA-DEPENDENT on
/// <c>Kind</c> — see <see cref="RemarkTemplateAccessGuard"/>'s remarks.
/// </summary>
/// <remarks>
/// <paramref name="timeProvider" />'s clock reading, not <see cref="RemarkTemplate.CreatedAtUtc"/>,
/// becomes the response's <c>createdAt</c>: <c>AuditingInterceptor</c> stamps
/// <see cref="RemarkTemplate.CreatedAtUtc"/> during <c>SaveChangesAsync</c>, which
/// <c>UnitOfWorkBehavior</c> runs AFTER this handler already returns its <see cref="Result"/> — so
/// the entity's own property is still the CLR default at the point this method builds the DTO.
/// Reading the clock here instead, the same way every other handler reads "now," sidesteps that
/// ordering rather than reporting a wrong timestamp.
/// </remarks>
internal sealed class CreateRemarkTemplateHandler(
    IRemarkTemplateRepository templates,
    IEffectivePrivilegeProvider grantsProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CreateRemarkTemplateCommand, Result<RemarkTemplateDto>>
{
    private const string EntityType = "remark_template";

    /// <inheritdoc />
    public async Task<Result<RemarkTemplateDto>> HandleAsync(CreateRemarkTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<RemarkTemplateDto>(
                Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!RemarkTemplateAccessGuard.HasPrivilegeForKind(grants, request.Kind))
        {
            return Result.Failure<RemarkTemplateDto>(Error.Forbidden(
                "remark_template.create_forbidden", $"You do not hold the privilege required to add a {request.Kind} template."));
        }

        var trimmedKey = request.Text.Trim().ToLowerInvariant();

        if (await templates.ExistsWithTextAsync(request.Kind, trimmedKey, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<RemarkTemplateDto>(Error.Conflict(
                "remark_template.duplicate", $"A {request.Kind} template with that text already exists."));
        }

        var creation = RemarkTemplate.Create(Guid.CreateVersion7(), request.Kind, request.Text);

        if (creation.IsFailure)
        {
            return Result.Failure<RemarkTemplateDto>(creation.Error);
        }

        var template = creation.Value;
        await templates.AddAsync(template, cancellationToken).ConfigureAwait(false);

        var createdAt = timeProvider.GetUtcNow();

        await auditSink.RecordAsync(
            RemarkTemplateAccessGuard.PrivilegeFor(request.Kind),
            EntityType,
            template.Id.ToString("D", CultureInfo.InvariantCulture),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = template.Kind.ToString(), ["text"] = template.Text },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            reason: null,
            beforeMetadata: null)
            .ConfigureAwait(false);

        return Result.Success(new RemarkTemplateDto(
            template.Id.ToString("D", CultureInfo.InvariantCulture), template.Kind, template.Text, createdAt));
    }
}
