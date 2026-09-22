using System.Collections.Concurrent;
using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Pins;

/// <summary>Maps batches and pins to their DTOs.</summary>
internal static class PinMapper
{
    public static PinBatchDto ToDto(PinBatch batch, PinBatchCounts? counts) => new(
        Id(batch.Id),
        Id(batch.SessionId),
        batch.Name,
        batch.PurposeNote,
        batch.PinLength,
        batch.MaxUses,
        batch.PinCount,
        counts?.Used ?? 0,
        counts?.Exhausted ?? 0,
        counts?.Suspended ?? 0,
        counts?.Revoked ?? 0,
        batch.State,
        batch.GeneratedAtUtc,
        batch.PlaintextPurgeAtUtc,
        batch.RevokeReason);

    public static PinSummaryDto ToDto(Pin pin) =>
        new(Id(pin.Id), pin.Prefix, pin.State, pin.UseCount, pin.MaxUses, pin.DistinctPupilCount, pin.StateReason);

    public static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    public static Guid? Actor(ICurrentUser currentUser) => currentUser.UserId is { } id ? Guid.Parse(id) : null;
}

/// <summary>Handles <see cref="GeneratePinBatchCommand"/> (spec 6.8.9).</summary>
internal sealed class GeneratePinBatchHandler(
    IPinBatchRepository batches,
    IPinSecrets secrets,
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<GeneratePinBatchCommand, Result<PinBatchDto>>
{
    /// <summary>Spec 6.8.6: a collision is retried up to five times before the whole generation fails.</summary>
    private const int MaxCollisionRounds = 5;

    /// <inheritdoc />
    public async Task<Result<PinBatchDto>> HandleAsync(GeneratePinBatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await sessions.FindReadOnlyByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<PinBatchDto>(Error.NotFound("session.not_found", "No session was found with that id."));
        }

        if (session.State == SessionState.Closed)
        {
            return Result.Failure<PinBatchDto>(Error.Conflict("pin_batch.session_closed", "This session is closed. Pins for it would never work."));
        }

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? await DefaultNameAsync(session, cancellationToken).ConfigureAwait(false)
            : request.Name.Trim();
        if (await batches.NameExistsInSessionAsync(session.Id, name, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PinBatchDto>(Error.Conflict("pin_batch.name_taken", $"A batch named {name} already exists in this session."));
        }

        var pinLength = request.PinLength ?? PinBatch.DefaultPinLength;
        var maxUses = request.MaxUses ?? PinBatch.DefaultMaxUses;
        var pinCount = request.PinCount!.Value;

        var generated = await GenerateUniqueAsync(pinCount, pinLength, cancellationToken).ConfigureAwait(false);
        if (generated is null)
        {
            return Result.Failure<PinBatchDto>(Error.Conflict("pin_batch.generation_failed", "Pin generation failed. Try again."));
        }

        var now = timeProvider.GetUtcNow();
        var batch = PinBatch.Create(Guid.CreateVersion7(), session.Id, name, request.PurposeNote, pinLength, maxUses, pinCount, now, PinMapper.Actor(currentUser));
        var pins = generated
            .Select(item => Pin.Create(Guid.CreateVersion7(), batch.Id, item.Material.PinHash, item.Material.LookupKey, item.Value[..Pin.PrefixLength], item.Material.Ciphertext, maxUses))
            .ToList();
        await batches.AddAsync(batch, pins, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Pin.Generate,
            "pin_batch",
            PinMapper.Id(batch.Id),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = batch.Name,
                ["sessionId"] = batch.SessionId,
                ["pinCount"] = pinCount,
                ["pinLength"] = pinLength,
                ["maxUses"] = maxUses,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(PinMapper.ToDto(batch, counts: null));
    }

    private async Task<string> DefaultNameAsync(AcademicSession session, CancellationToken cancellationToken)
    {
        var sessionTerms = await terms.ListBySessionReadOnlyAsync(session.Id, cancellationToken).ConfigureAwait(false);
        var term = sessionTerms.FirstOrDefault(candidate => candidate.State == TermState.Active)
            ?? sessionTerms.OrderBy(candidate => candidate.Ordinal).FirstOrDefault(candidate => candidate.State == TermState.Upcoming);
        var sequence = await batches.CountInSessionAsync(session.Id, cancellationToken).ConfigureAwait(false) + 1;
        var label = term is null ? session.Name : $"{session.Name} {term.Name}";
        return $"{label} batch {sequence.ToString(CultureInfo.InvariantCulture)}";
    }

    private async Task<List<(string Value, PinSecretMaterial Material)>?> GenerateUniqueAsync(int count, int length, CancellationToken cancellationToken)
    {
        var accepted = new List<(string Value, PinSecretMaterial Material)>(count);
        var acceptedKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var round = 0; round < MaxCollisionRounds && accepted.Count < count; round++)
        {
            var needed = count - accepted.Count;
            var candidates = new ConcurrentBag<(string Value, PinSecretMaterial Material)>();
            Parallel.For(0, needed, new ParallelOptions { CancellationToken = cancellationToken }, _ =>
            {
                var value = PinValue.Generate(length);
                candidates.Add((value, secrets.Protect(value)));
            });

            var fresh = candidates.Where(candidate => acceptedKeys.Add(candidate.Material.LookupKey)).ToList();
            var taken = await batches.FindExistingLookupKeysAsync(fresh.Select(candidate => candidate.Material.LookupKey).ToList(), cancellationToken).ConfigureAwait(false);
            foreach (var candidate in fresh)
            {
                if (taken.Contains(candidate.Material.LookupKey))
                {
                    acceptedKeys.Remove(candidate.Material.LookupKey);
                    continue;
                }

                accepted.Add(candidate);
            }
        }

        return accepted.Count == count ? accepted : null;
    }
}

/// <summary>Handles <see cref="ListPinBatchesQuery"/>.</summary>
internal sealed class ListPinBatchesHandler(IPinBatchRepository batches) : IRequestHandler<ListPinBatchesQuery, Result<CursorPage<PinBatchDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<PinBatchDto>>> HandleAsync(ListPinBatchesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageSize = Math.Clamp(request.PageSize ?? CursorPageRequest.DefaultPageSize, 1, CursorPageRequest.MaxPageSize);
        var cursor = request.Cursor is null ? (Guid?)null : Guid.Parse(request.Cursor);

        var page = await batches.ListAsync(request.SessionId, request.State, cursor, pageSize + 1, cancellationToken).ConfigureAwait(false);
        var items = page.Take(pageSize).ToList();
        var counts = await batches.CountPinsAsync(items.Select(batch => batch.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var dtos = items.Select(batch => PinMapper.ToDto(batch, counts.GetValueOrDefault(batch.Id))).ToList();
        var next = page.Count > pageSize ? PinMapper.Id(items[^1].Id) : null;
        return Result.Success(new CursorPage<PinBatchDto>(dtos, next));
    }
}

/// <summary>Handles <see cref="GetPinBatchQuery"/>.</summary>
internal sealed class GetPinBatchHandler(IPinBatchRepository batches) : IRequestHandler<GetPinBatchQuery, Result<PinBatchDetailDto>>
{
    /// <inheritdoc />
    public async Task<Result<PinBatchDetailDto>> HandleAsync(GetPinBatchQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await batches.FindReadOnlyAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PinBatchDetailDto>(Error.NotFound("pin_batch.not_found", "No pin batch was found with that id."));
        }

        var pins = await batches.ListPinsAsync(batch.Id, tracked: false, cancellationToken).ConfigureAwait(false);
        var counts = await batches.CountPinsAsync([batch.Id], cancellationToken).ConfigureAwait(false);
        return Result.Success(new PinBatchDetailDto(PinMapper.ToDto(batch, counts.GetValueOrDefault(batch.Id)), pins.Select(PinMapper.ToDto).ToList()));
    }
}

/// <summary>Handles <see cref="MarkPinBatchDistributedCommand"/>.</summary>
internal sealed class MarkPinBatchDistributedHandler(IPinBatchRepository batches, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<MarkPinBatchDistributedCommand, Result<PinBatchDto>>
{
    /// <inheritdoc />
    public async Task<Result<PinBatchDto>> HandleAsync(MarkPinBatchDistributedCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await batches.FindTrackedAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PinBatchDto>(Error.NotFound("pin_batch.not_found", "No pin batch was found with that id."));
        }

        var before = batch.State;
        var result = batch.MarkDistributed();
        if (result.IsFailure)
        {
            return Result.Failure<PinBatchDto>(result.Error);
        }

        await PinAudit.StateChangeAsync(auditSink, currentUser, "pin_batch.distributed", "pin_batch", batch.Id, before.ToString(), batch.State.ToString(), reason: null, cancellationToken).ConfigureAwait(false);
        var counts = await batches.CountPinsAsync([batch.Id], cancellationToken).ConfigureAwait(false);
        return Result.Success(PinMapper.ToDto(batch, counts.GetValueOrDefault(batch.Id)));
    }
}

/// <summary>Handles <see cref="RevokePinBatchCommand"/>: the batch and every pin in it, in one transaction.</summary>
internal sealed class RevokePinBatchHandler(IPinBatchRepository batches, ICurrentUser currentUser, ISystemAuditSink auditSink, TimeProvider timeProvider)
    : IRequestHandler<RevokePinBatchCommand, Result<PinBatchDto>>
{
    /// <inheritdoc />
    public async Task<Result<PinBatchDto>> HandleAsync(RevokePinBatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await batches.FindTrackedAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PinBatchDto>(Error.NotFound("pin_batch.not_found", "No pin batch was found with that id."));
        }

        var now = timeProvider.GetUtcNow();
        var actor = PinMapper.Actor(currentUser);
        var before = batch.State;
        var result = batch.Revoke(request.Reason, actor, now);
        if (result.IsFailure)
        {
            return Result.Failure<PinBatchDto>(result.Error);
        }

        var pins = await batches.ListPinsAsync(batch.Id, tracked: true, cancellationToken).ConfigureAwait(false);
        foreach (var pin in pins.Where(pin => pin.State != PinState.Revoked))
        {
            pin.Revoke(request.Reason, actor, now);
        }

        await PinAudit.StateChangeAsync(auditSink, currentUser, Privileges.Pin.Revoke, "pin_batch", batch.Id, before.ToString(), batch.State.ToString(), request.Reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(PinMapper.ToDto(batch, new PinBatchCounts(pins.Count(pin => pin.UseCount > 0), 0, 0, pins.Count)));
    }
}

/// <summary>Handles <see cref="RevokePinCommand"/>.</summary>
internal sealed class RevokePinHandler(IPinBatchRepository batches, ICurrentUser currentUser, ISystemAuditSink auditSink, TimeProvider timeProvider)
    : IRequestHandler<RevokePinCommand, Result<PinSummaryDto>>
{
    /// <inheritdoc />
    public async Task<Result<PinSummaryDto>> HandleAsync(RevokePinCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pin = await batches.FindPinTrackedAsync(request.PinId, cancellationToken).ConfigureAwait(false);
        if (pin is null)
        {
            return Result.Failure<PinSummaryDto>(Error.NotFound("pin.not_found", "No pin was found with that id."));
        }

        var before = pin.State;
        var result = pin.Revoke(request.Reason, PinMapper.Actor(currentUser), timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return Result.Failure<PinSummaryDto>(result.Error);
        }

        await PinAudit.StateChangeAsync(auditSink, currentUser, Privileges.Pin.Revoke, "pin", pin.Id, before.ToString(), pin.State.ToString(), request.Reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(PinMapper.ToDto(pin));
    }
}

/// <summary>Handles <see cref="ReinstatePinCommand"/>.</summary>
internal sealed class ReinstatePinHandler(IPinBatchRepository batches, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<ReinstatePinCommand, Result<PinSummaryDto>>
{
    /// <inheritdoc />
    public async Task<Result<PinSummaryDto>> HandleAsync(ReinstatePinCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pin = await batches.FindPinTrackedAsync(request.PinId, cancellationToken).ConfigureAwait(false);
        if (pin is null)
        {
            return Result.Failure<PinSummaryDto>(Error.NotFound("pin.not_found", "No pin was found with that id."));
        }

        var before = pin.State;
        var result = pin.Reinstate();
        if (result.IsFailure)
        {
            return Result.Failure<PinSummaryDto>(result.Error);
        }

        await PinAudit.StateChangeAsync(auditSink, currentUser, "pin.reinstate", "pin", pin.Id, before.ToString(), pin.State.ToString(), request.Reason, cancellationToken).ConfigureAwait(false);
        return Result.Success(PinMapper.ToDto(pin));
    }
}

/// <summary>The audit event every pin state change writes. Pin values never appear here.</summary>
internal static class PinAudit
{
    public static Task StateChangeAsync(
        ISystemAuditSink auditSink, ICurrentUser currentUser, string action, string entityType, Guid entityId,
        string beforeState, string afterState, string? reason, CancellationToken cancellationToken)
    {
        var after = new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = afterState };
        if (reason is not null)
        {
            after["reason"] = reason.Trim();
        }

        return auditSink.RecordAsync(
            action,
            entityType,
            PinMapper.Id(entityId),
            metadata: after,
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = beforeState });
    }
}
