using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// <c>DELETE /api/v1/subject-exceptions/{id}</c> (spec 6.6.4, 6.6.9). TASK-0070 delta amendment 2:
/// requires <c>subject.map.arm</c> — the same privilege as its create, NOT <c>subject.unmap</c>
/// (deleting an exception is not "ending a mapping"). No closed-session 409: spec 6.6.8 states the
/// closed-session rejection for CREATION only.
/// </summary>
/// <param name="Id">The exception to delete.</param>
public sealed record DeleteSubjectExceptionCommand(Guid Id) : ICommand<Result>;

/// <summary>Nothing to validate beyond the route-bound <see cref="Guid"/>.</summary>
internal sealed class DeleteSubjectExceptionCommandValidator : AbstractValidator<DeleteSubjectExceptionCommand>
{
}
