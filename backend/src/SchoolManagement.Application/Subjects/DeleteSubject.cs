using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary><c>DELETE /api/v1/subjects/{id}</c> (spec 6.6.8, 6.6.9).</summary>
/// <param name="Id">The subject to delete.</param>
public sealed record DeleteSubjectCommand(Guid Id) : ICommand<Result>;

/// <summary>Nothing to validate beyond the route-bound <see cref="Guid"/>.</summary>
internal sealed class DeleteSubjectCommandValidator : AbstractValidator<DeleteSubjectCommand>
{
}
