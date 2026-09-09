using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Audit;

/// <summary>Handles <see cref="ExportAuditEventsCommand"/>.</summary>
/// <remarks>
/// <see cref="ISystemAuditSink.RecordAsync"/> only adds to the ambient change tracker — it does not
/// save. <see cref="SchoolManagement.Application.Behaviors.UnitOfWorkBehavior{TRequest,TResponse}"/>
/// commits that INSERT (because this is an <see cref="ICommand{TResponse}"/>) before this handler's
/// caller ever sees the returned stream, which is what makes "an export that does not log itself
/// fails this card" true by construction: the self-log row is durable before a single CSV byte can
/// reach the client. The stream itself is built (not enumerated) here — its SELECT only runs when
/// the endpoint later iterates it, safely AFTER that commit, over the same scoped <c>DbContext</c>.
/// </remarks>
internal sealed class ExportAuditEventsCommandHandler(
    IAuditEventQueryRepository auditEvents,
    ISystemAuditSink auditSink,
    ICurrentUser currentUser)
    : IRequestHandler<ExportAuditEventsCommand, Result<IAsyncEnumerable<AuditEventDto>>>
{
    /// <summary>The self-log row's own <c>entity_type</c> — this event describes the audit trail itself.</summary>
    private const string SelfLogEntityType = "audit_event";

    /// <inheritdoc />
    public async Task<Result<IAsyncEnumerable<AuditEventDto>>> HandleAsync(
        ExportAuditEventsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await auditSink.RecordAsync(
            Privileges.Audit.Export,
            SelfLogEntityType,
            entityId: null,
            BuildFilterMetadata(request),
            currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        var stream = auditEvents.StreamAsync(
            request.FromUtc,
            request.ToUtc,
            request.ActorAdminId,
            request.Action,
            request.EntityType,
            request.Outcome,
            cancellationToken);

        return Result.Success(stream);
    }

    /// <summary>
    /// Every filter key is present, even when its value is null, so an unfiltered export (the whole
    /// log) and one narrowed by, say, <c>entityType</c> are visibly DIFFERENT rows — spec 6.1.12's
    /// "who exported what" is answerable only if a filter's absence is recorded, not merely its
    /// presence.
    /// </summary>
    private static Dictionary<string, object?> BuildFilterMetadata(ExportAuditEventsCommand request) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fromUtc"] = request.FromUtc,
            ["toUtc"] = request.ToUtc,
            ["actorAdminId"] = request.ActorAdminId?.ToString("D", CultureInfo.InvariantCulture),
            ["action"] = request.Action,
            ["entityType"] = request.EntityType,
            ["outcome"] = request.Outcome?.ToString(),
        };
}
