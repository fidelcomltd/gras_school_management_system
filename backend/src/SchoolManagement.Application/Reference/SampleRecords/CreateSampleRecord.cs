using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Reference;

namespace SchoolManagement.Application.Reference.SampleRecords;

/// <summary>
/// REFERENCE SLICE — the minimal COMMAND. Copy this shape for anything that changes state.
/// </summary>
/// <remarks>
/// Being an <see cref="ICommand{TResponse}"/> is what gets it a transaction: the unit-of-work
/// behaviour is constrained to <see cref="IBaseCommand"/>, so declaring the interface is the only
/// wiring needed. Nothing else opts in.
/// </remarks>
/// <param name="Label">A short label for the record. Required, unique among live records.</param>
/// <param name="Note">An optional free-text note.</param>
public sealed record CreateSampleRecordCommand(string Label, string? Note)
    : ICommand<Result<CreateSampleRecordResponse>>;

/// <summary>Response to a successful <see cref="CreateSampleRecordCommand"/>.</summary>
/// <param name="Id">
/// The new record's opaque identifier. Returned in the body as well as in the <c>Location</c>
/// header, so a client need not parse the URL to learn it.
/// </param>
public sealed record CreateSampleRecordResponse(string Id);

/// <summary>
/// Validates <see cref="CreateSampleRecordCommand"/>.
/// </summary>
/// <remarks>
/// The length limits deliberately reference the DOMAIN constants rather than repeating literals, so
/// the validator, the entity invariant, and the database column length cannot drift apart. If you
/// find yourself typing a number here that also appears in an entity configuration, reference a
/// constant instead.
/// </remarks>
internal sealed class CreateSampleRecordCommandValidator : AbstractValidator<CreateSampleRecordCommand>
{
    /// <summary>Configures the rules.</summary>
    public CreateSampleRecordCommandValidator()
    {
        RuleFor(command => command.Label)
            .NotEmpty()
            .WithMessage("Label is required.")
            .MaximumLength(SampleRecord.LabelMaxLength)
            .WithMessage($"Label must be at most {SampleRecord.LabelMaxLength} characters.");

        RuleFor(command => command.Note)
            .MaximumLength(SampleRecord.NoteMaxLength)
            .WithMessage($"Note must be at most {SampleRecord.NoteMaxLength} characters.")
            .When(command => command.Note is not null);
    }
}
