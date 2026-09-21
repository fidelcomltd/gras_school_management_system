using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>POST /api/v1/remark-templates</c> (TASK-0086 stage B; spec §6.7.7 delta item 4).
/// <c>Idempotency-Key</c> is REQUIRED — unlike a PUT sheet save, there is no version to make a
/// retry self-correcting, same posture <c>CreateSectionCommand</c> takes.
/// </summary>
/// <param name="Kind">Which list this phrase joins. Also decides the privilege — see <see cref="RemarkTemplateAccessGuard"/>.</param>
/// <param name="Text">1-300 characters after trimming, else 422. A duplicate within <paramref name="Kind"/> (trimmed, case-insensitive) is 409.</param>
public sealed record CreateRemarkTemplateCommand(RemarkKind Kind, string Text) : ICommand<Result<RemarkTemplateDto>>;

/// <summary>Structural check only — the duplicate check is data-dependent and lives in the handler.</summary>
internal sealed class CreateRemarkTemplateCommandValidator : AbstractValidator<CreateRemarkTemplateCommand>
{
    public CreateRemarkTemplateCommandValidator() =>
        RuleFor(command => command.Text).NotEmpty().MaximumLength(RemarkTemplate.TextMaxLength);
}
