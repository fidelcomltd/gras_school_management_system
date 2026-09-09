using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Domain.Audit;

namespace SchoolManagement.Infrastructure.Audit;

/// <summary>
/// Builds an <see cref="AuditEvent"/> from the arguments both <c>ISystemAuditSink</c> and
/// <c>IAuthorizationAuditSink</c> implementations receive, resolving every ambient field (spec
/// 6.1.12: <c>actor_label</c>, <c>source_ip</c>, <c>user_agent</c>) so NEITHER of the ~36 existing
/// call sites across the codebase has to supply them (TASK-0048's card).
/// </summary>
/// <remarks>
/// Shared by <c>SystemAuditSink</c> and <c>AuthorizationAuditSink</c> rather than duplicated —
/// an Infrastructure-internal implementation detail, not a third audit abstraction (neither
/// Application-layer interface changes shape because of this type).
/// </remarks>
internal sealed class AuditEventFactory(
    IAdminAccountRepository accounts,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string SystemActorLabel = "System";
    private const string UnknownEntityType = "unspecified";

    /// <summary>Label used when the actor id parses but no such account exists any more.</summary>
    private const string UnknownAccountLabel = "(unknown account)";

    public async Task<AuditEvent> BuildAsync(
        AuditOutcome outcome,
        string action,
        string? entityType,
        string? entityId,
        IReadOnlyDictionary<string, object?>? metadata,
        string? actorAdminIdText,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        Guid? actorId = actorAdminIdText is not null && Guid.TryParse(actorAdminIdText, out var parsed)
            ? parsed
            : null;

        var actorLabel = await ResolveActorLabelAsync(actorId, cancellationToken).ConfigureAwait(false);

        var afterJson = metadata is null || metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata);

        return AuditEvent.Create(
            timeProvider.GetUtcNow(),
            actorId,
            actorLabel,
            action,
            entityType ?? UnknownEntityType,
            entityId,
            outcome,
            beforeJson: null,
            afterJson,
            reason,
            AuditFieldTruncation.TruncateSourceIp(currentUser.RemoteIpAddress),
            AuditFieldTruncation.TruncateUserAgent(currentUser.UserAgent));
    }

    private async Task<string> ResolveActorLabelAsync(Guid? actorId, CancellationToken cancellationToken)
    {
        if (actorId is not { } id)
        {
            return SystemActorLabel;
        }

        var account = await accounts.FindReadOnlyByIdAsync(id, cancellationToken).ConfigureAwait(false);

        return account is null
            ? UnknownAccountLabel
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{account.StaffName} <{account.Email}>");
    }
}
