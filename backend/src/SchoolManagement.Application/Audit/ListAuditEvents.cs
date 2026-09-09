using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Audit;

/// <summary>
/// <c>GET /api/v1/audit-events</c> (spec 6.1.12): "filterable by date range, actor, action, entity
/// type and outcome, and sorted newest first by default." Cursor-paginated per spec 9.5 — never
/// offset. Every filter is optional and combinable.
/// </summary>
/// <param name="Cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
/// <param name="PageSize">Defaults to <see cref="CursorPageRequest.DefaultPageSize"/>, capped at <see cref="CursorPageRequest.MaxPageSize"/>.</param>
/// <param name="FromUtc">Inclusive lower bound on <c>occurred_at</c>, or <see langword="null"/> for no lower bound.</param>
/// <param name="ToUtc">Inclusive upper bound on <c>occurred_at</c>, or <see langword="null"/> for no upper bound.</param>
/// <param name="ActorAdminId">Exact match on the acting administrator, or <see langword="null"/> for any actor.</param>
/// <param name="Action">Exact match on the privilege/action string, or <see langword="null"/> for any action.</param>
/// <param name="EntityType">Exact match on the affected entity's type, or <see langword="null"/> for any.</param>
/// <param name="Outcome">Exact match on <c>Success</c>/<c>Rejected</c>, or <see langword="null"/> for both.</param>
public sealed record ListAuditEventsQuery(
    string? Cursor,
    int? PageSize,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? ActorAdminId,
    string? Action,
    string? EntityType,
    AuditOutcome? Outcome)
    : IQuery<Result<CursorPage<AuditEventDto>>>;

/// <summary>Validates <see cref="ListAuditEventsQuery"/>.</summary>
internal sealed class ListAuditEventsQueryValidator : AbstractValidator<ListAuditEventsQuery>
{
    public ListAuditEventsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(CursorPageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {CursorPageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);

        RuleFor(query => query.Outcome)
            .IsInEnum();

        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .WithMessage("toUtc must not be before fromUtc.")
            .When(query => query.FromUtc is not null && query.ToUtc is not null);
    }
}
