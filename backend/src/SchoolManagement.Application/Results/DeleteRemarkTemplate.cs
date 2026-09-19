using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary><c>DELETE /api/v1/remark-templates/{id}</c> (TASK-0086 stage B; spec §6.7.7 delta item 4) — a hard delete.</summary>
/// <param name="Id">The template to remove.</param>
public sealed record DeleteRemarkTemplateCommand(Guid Id) : ICommand<Result>;

/// <summary>Nothing to validate beyond the route-bound <see cref="Guid"/>.</summary>
internal sealed class DeleteRemarkTemplateCommandValidator : AbstractValidator<DeleteRemarkTemplateCommand>
{
}
