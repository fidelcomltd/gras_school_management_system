using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>DELETE /api/v1/arms/{id}</c> (spec 6.4.7, 6.4.9): "Permitted only where no enrolment has ever
/// existed." Enrolment is Phase 2 and does not exist in this codebase yet — see
/// <see cref="DeleteArmHandler"/>'s remarks for what that means today.
/// </summary>
/// <param name="Id">The arm to delete.</param>
public sealed record DeleteArmCommand(Guid Id) : ICommand<Result>;

/// <summary>Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.</summary>
internal sealed class DeleteArmCommandValidator : AbstractValidator<DeleteArmCommand>;
