using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// <c>POST /api/v1/terms/{id}/reopen</c> (spec 6.3.6): Super Admin only, a reason of at least
/// <see cref="Term.ReopenReasonMinLength"/> characters, refused outright if the following term has
/// already been opened.
/// </summary>
/// <param name="Id">The closed term being reopened.</param>
/// <param name="Reason">At least 10 characters — spec 6.3.6's audit trail for why marks moved after close.</param>
public sealed record ReopenTermCommand(Guid Id, string Reason) : ICommand<Result<TermDto>>;

/// <summary>Validates <see cref="ReopenTermCommand"/>.</summary>
internal sealed class ReopenTermCommandValidator : AbstractValidator<ReopenTermCommand>
{
    public ReopenTermCommandValidator() =>
        RuleFor(command => command.Reason)
            .NotEmpty()
            .MinimumLength(Term.ReopenReasonMinLength)
            .WithMessage($"A reason of at least {Term.ReopenReasonMinLength} characters is required.");
}
