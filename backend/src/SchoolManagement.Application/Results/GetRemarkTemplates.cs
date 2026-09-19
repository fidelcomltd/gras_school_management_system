using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/remark-templates?kind=</c> (TASK-0086 stage B; spec §6.7.7 delta item 4) — one
/// kind's saved-phrase list, in creation order.
/// </summary>
/// <param name="Kind">Required. Bound from the query string as an enum, so an unknown value 400s before this even runs.</param>
public sealed record GetRemarkTemplatesQuery(RemarkKind Kind) : IQuery<Result<RemarkTemplateListDto>>;

/// <summary>Nothing to validate — <see cref="RemarkKind"/> model binding already rejected an unknown value.</summary>
internal sealed class GetRemarkTemplatesQueryValidator : AbstractValidator<GetRemarkTemplatesQuery>
{
}
