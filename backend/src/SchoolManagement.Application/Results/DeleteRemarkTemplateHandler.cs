using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Handles <see cref="DeleteRemarkTemplateCommand"/>. PRIVILEGE IS DATA-DEPENDENT — see
/// <see cref="RemarkTemplateAccessGuard"/>'s remarks.
/// </summary>
/// <remarks>
/// ORDER MATTERS (spec delta item 4): a caller holding NEITHER remark-template privilege is
/// refused BEFORE the id is even looked up, so it never learns whether the id exists (403, never
/// 404). A caller holding one but not the other is let through the lookup, and is then refused with
/// 403 — never 404, and the row is never touched — if the row turns out to be the OTHER kind.
/// </remarks>
internal sealed class DeleteRemarkTemplateHandler(
    IRemarkTemplateRepository templates,
    IEffectivePrivilegeProvider grantsProvider,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<DeleteRemarkTemplateCommand, Result>
{
    private const string EntityType = "remark_template";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(DeleteRemarkTemplateCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!RemarkTemplateAccessGuard.HasPrivilegeForAnyKind(grants))
        {
            return Result.Failure(Error.Forbidden(
                "remark_template.delete_forbidden", "You do not hold either remark-template privilege."));
        }

        var template = await templates.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (template is null)
        {
            return Result.Failure(Error.NotFound("remark_template.not_found", "No remark template was found with that id."));
        }

        if (!RemarkTemplateAccessGuard.HasPrivilegeForKind(grants, template.Kind))
        {
            return Result.Failure(Error.Forbidden(
                "remark_template.delete_forbidden", $"You do not hold the privilege required to delete a {template.Kind} template."));
        }

        await templates.RemoveAsync(template, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            RemarkTemplateAccessGuard.PrivilegeFor(template.Kind),
            EntityType,
            template.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken,
            reason: null,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = template.Kind.ToString(), ["text"] = template.Text })
            .ConfigureAwait(false);

        return Result.Success();
    }
}
