using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;

namespace SchoolManagement.Application.Pins;

/// <summary>A batch as the list and detail views show it (spec 6.8.11). Never carries a pin value.</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="SessionId">The session the pins are valid for.</param>
/// <param name="Name">Unique within the session.</param>
/// <param name="PurposeNote">Informational; restricts nothing.</param>
/// <param name="PinLength">Characters per pin.</param>
/// <param name="MaxUses">Uses per pin.</param>
/// <param name="PinCount">Pins generated.</param>
/// <param name="PinsUsed">Pins used at least once.</param>
/// <param name="PinsExhausted">Pins with no uses left.</param>
/// <param name="PinsSuspended">Pins suspended by the spread control.</param>
/// <param name="PinsRevoked">Pins revoked.</param>
/// <param name="State">Spec 6.8.10.</param>
/// <param name="GeneratedAt">When generated.</param>
/// <param name="PlaintextPurgeAt">Reprinting is possible until this moment.</param>
/// <param name="RevokeReason">Set once revoked.</param>
public sealed record PinBatchDto(
    string Id,
    string SessionId,
    string Name,
    string? PurposeNote,
    int PinLength,
    int MaxUses,
    int PinCount,
    int PinsUsed,
    int PinsExhausted,
    int PinsSuspended,
    int PinsRevoked,
    PinBatchState State,
    DateTimeOffset GeneratedAt,
    DateTimeOffset PlaintextPurgeAt,
    string? RevokeReason);

/// <summary>One pin in a batch's detail view: its prefix and position, never its value (spec 6.8.13).</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="Prefix">The first four characters, for identifying a slip read out over the telephone.</param>
/// <param name="State">Spec 6.8.5.</param>
/// <param name="UseCount">Uses spent.</param>
/// <param name="MaxUses">Uses allowed.</param>
/// <param name="DistinctPupilCount">Different pupils opened.</param>
/// <param name="StateReason">Why it was suspended or revoked.</param>
public sealed record PinSummaryDto(string Id, string Prefix, PinState State, int UseCount, int MaxUses, int DistinctPupilCount, string? StateReason);

/// <summary>A batch with its pins.</summary>
/// <param name="Batch">The batch.</param>
/// <param name="Pins">Oldest first.</param>
public sealed record PinBatchDetailDto(PinBatchDto Batch, IReadOnlyList<PinSummaryDto> Pins);

/// <summary>Generates a batch (spec 6.8.9). Pins are not tied to any pupil.</summary>
/// <param name="SessionId">Required; the session must not be closed.</param>
/// <param name="Name">Optional; defaults to the session and current term plus a sequence.</param>
/// <param name="PurposeNote">Optional, up to 200 characters.</param>
/// <param name="PinCount">1 to 2000.</param>
/// <param name="PinLength">10 to 16; default 10.</param>
/// <param name="MaxUses">1 to 100; default 3. Above 10, <paramref name="ConfirmMaxUses"/> must repeat it.</param>
/// <param name="ConfirmMaxUses">The typed-back confirmation spec 6.8.12 requires for a high maximum.</param>
public sealed record GeneratePinBatchCommand(
    Guid SessionId,
    string? Name,
    string? PurposeNote,
    int? PinCount,
    int? PinLength,
    int? MaxUses,
    int? ConfirmMaxUses) : ICommand<Result<PinBatchDto>>;

/// <summary>Spec 6.8.4 and 6.8.12 bounds, with the spec's own messages.</summary>
internal sealed class GeneratePinBatchCommandValidator : AbstractValidator<GeneratePinBatchCommand>
{
    public GeneratePinBatchCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.PinCount)
            .NotNull().WithMessage("Enter how many pins to generate.")
            .InclusiveBetween(1, PinBatch.MaxPinCount).WithMessage($"Enter how many pins to generate, from 1 to {PinBatch.MaxPinCount}.");
        RuleFor(command => command.PinLength)
            .InclusiveBetween(PinBatch.MinPinLength, PinBatch.MaxPinLength)
            .When(command => command.PinLength is not null)
            .WithMessage($"Pin length must be between {PinBatch.MinPinLength} and {PinBatch.MaxPinLength}.");
        RuleFor(command => command.MaxUses)
            .InclusiveBetween(PinBatch.MinMaxUses, PinBatch.MaxMaxUses)
            .When(command => command.MaxUses is not null)
            .WithMessage($"Maximum uses must be between {PinBatch.MinMaxUses} and {PinBatch.MaxMaxUses}.");
        RuleFor(command => command.ConfirmMaxUses)
            .Equal(command => command.MaxUses)
            .When(command => command.MaxUses > PinBatch.MaxUsesConfirmationThreshold)
            .WithMessage(command =>
                $"You are about to generate pins that can each open {command.MaxUses} pupils' results. Type the number to confirm.");
        RuleFor(command => command.Name).MaximumLength(PinBatch.NameMaxLength);
        RuleFor(command => command.PurposeNote).MaximumLength(PinBatch.PurposeNoteMaxLength);
    }
}

/// <summary>Lists batches newest first (spec 6.8.11), cursor-paged.</summary>
/// <param name="SessionId">Optional filter.</param>
/// <param name="State">Optional filter.</param>
/// <param name="Cursor">The previous page's last id.</param>
/// <param name="PageSize">Clamped to 1..100; default 25.</param>
public sealed record ListPinBatchesQuery(Guid? SessionId, PinBatchState? State, string? Cursor, int? PageSize) : IQuery<Result<CursorPage<PinBatchDto>>>;

/// <summary>The cursor must be an id.</summary>
internal sealed class ListPinBatchesQueryValidator : AbstractValidator<ListPinBatchesQuery>
{
    public ListPinBatchesQueryValidator() =>
        RuleFor(query => query.Cursor).Must(cursor => Guid.TryParse(cursor, out _)).When(query => query.Cursor is not null)
            .WithMessage("Cursor is not valid.");
}

/// <summary>One batch with its pins.</summary>
/// <param name="BatchId">From the route.</param>
public sealed record GetPinBatchQuery(Guid BatchId) : IQuery<Result<PinBatchDetailDto>>;

/// <summary>No input beyond the route.</summary>
internal sealed class GetPinBatchQueryValidator : AbstractValidator<GetPinBatchQuery>;

/// <summary>Generated or Printed to Active (spec 6.8.9 step 8).</summary>
/// <param name="BatchId">From the route.</param>
public sealed record MarkPinBatchDistributedCommand(Guid BatchId) : ICommand<Result<PinBatchDto>>;

/// <summary>No input beyond the route.</summary>
internal sealed class MarkPinBatchDistributedCommandValidator : AbstractValidator<MarkPinBatchDistributedCommand>;

/// <summary>A reason, for every revoke and reinstate (spec 6.8.4, 6.8.13).</summary>
/// <param name="Reason">1 to 500 characters once trimmed.</param>
public sealed record PinReasonRequest(string Reason);

/// <summary>Revokes a batch and every pin in it (spec 6.8.10).</summary>
/// <param name="BatchId">From the route.</param>
/// <param name="Reason">Required.</param>
public sealed record RevokePinBatchCommand(Guid BatchId, string Reason) : ICommand<Result<PinBatchDto>>;

/// <summary>Revokes one pin.</summary>
/// <param name="PinId">From the route.</param>
/// <param name="Reason">Required.</param>
public sealed record RevokePinCommand(Guid PinId, string Reason) : ICommand<Result<PinSummaryDto>>;

/// <summary>Clears a spread-control suspension.</summary>
/// <param name="PinId">From the route.</param>
/// <param name="Reason">Required.</param>
public sealed record ReinstatePinCommand(Guid PinId, string Reason) : ICommand<Result<PinSummaryDto>>;

/// <summary>Reason bounds shared by the three reasoned commands.</summary>
internal static class PinReasonRules
{
    public static void Apply<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string>> reason) =>
        validator.RuleFor(reason)
            .Must(value => value is not null && value.Trim().Length is >= 1 and <= Pin.ReasonMaxLength)
            .WithMessage($"Give a reason of up to {Pin.ReasonMaxLength} characters.");
}

/// <summary>Reason required.</summary>
internal sealed class RevokePinBatchCommandValidator : AbstractValidator<RevokePinBatchCommand>
{
    public RevokePinBatchCommandValidator() => PinReasonRules.Apply(this, command => command.Reason);
}

/// <summary>Reason required.</summary>
internal sealed class RevokePinCommandValidator : AbstractValidator<RevokePinCommand>
{
    public RevokePinCommandValidator() => PinReasonRules.Apply(this, command => command.Reason);
}

/// <summary>Reason required.</summary>
internal sealed class ReinstatePinCommandValidator : AbstractValidator<ReinstatePinCommand>
{
    public ReinstatePinCommandValidator() => PinReasonRules.Apply(this, command => command.Reason);
}
