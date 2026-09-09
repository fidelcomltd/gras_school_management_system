using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Audit;

/// <summary>
/// <c>GET /api/v1/audit-events/export</c> (spec 6.1.12): "Export to CSV requires <c>audit.export</c>
/// and is itself an audit event." A <see cref="ICommand{TResponse}"/>, not a query, precisely
/// because it WRITES that event — every call appends one <c>audit_event</c> row (action
/// <c>audit.export</c>, its <c>after_json</c> recording the filters actually used) before the
/// filtered set is streamed back. Same five filters as <see cref="ListAuditEventsQuery"/>, no
/// paging: the whole matching set is exported.
/// </summary>
public sealed record ExportAuditEventsCommand(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    Guid? ActorAdminId,
    string? Action,
    string? EntityType,
    AuditOutcome? Outcome)
    : ICommand<Result<IAsyncEnumerable<AuditEventDto>>>;

/// <summary>Validates <see cref="ExportAuditEventsCommand"/>. Same rules as the list query's.</summary>
internal sealed class ExportAuditEventsCommandValidator : AbstractValidator<ExportAuditEventsCommand>
{
    public ExportAuditEventsCommandValidator()
    {
        RuleFor(command => command.Outcome)
            .IsInEnum();

        RuleFor(command => command.ToUtc)
            .GreaterThanOrEqualTo(command => command.FromUtc)
            .WithMessage("toUtc must not be before fromUtc.")
            .When(command => command.FromUtc is not null && command.ToUtc is not null);
    }
}
