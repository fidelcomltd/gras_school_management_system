using System.Globalization;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Handles <see cref="GetRemarkTemplatesQuery"/>. PRIVILEGE IS DATA-DEPENDENT on <c>Kind</c> — see
/// <see cref="RemarkTemplateAccessGuard"/>'s remarks for why this is a handler-level check rather
/// than a route-declarative one.
/// </summary>
internal sealed class GetRemarkTemplatesHandler(
    IRemarkTemplateRepository templates,
    IEffectivePrivilegeProvider grantsProvider,
    ICurrentUser currentUser)
    : IRequestHandler<GetRemarkTemplatesQuery, Result<RemarkTemplateListDto>>
{
    /// <inheritdoc />
    public async Task<Result<RemarkTemplateListDto>> HandleAsync(GetRemarkTemplatesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<RemarkTemplateListDto>(
                Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var grants = await grantsProvider.GetGrantsAsync(userId, cancellationToken).ConfigureAwait(false);

        if (!RemarkTemplateAccessGuard.HasPrivilegeForKind(grants, request.Kind))
        {
            return Result.Failure<RemarkTemplateListDto>(Error.Forbidden(
                "remark_template.view_forbidden", $"You do not hold the privilege required to view {request.Kind} templates."));
        }

        var list = await templates.ListByKindReadOnlyAsync(request.Kind, cancellationToken).ConfigureAwait(false);

        return Result.Success(new RemarkTemplateListDto(list.Select(ToDto).ToArray()));
    }

    private static RemarkTemplateDto ToDto(RemarkTemplate template) => new(
        template.Id.ToString("D", CultureInfo.InvariantCulture), template.Kind, template.Text, template.CreatedAtUtc);
}
