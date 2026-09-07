using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Sessions;

/// <summary><c>POST /api/v1/terms/{id}/close</c> (spec 6.3.6). Moves active to closed.</summary>
/// <param name="Id">The term being closed.</param>
public sealed record CloseTermCommand(Guid Id) : ICommand<Result<TermDto>>;

/// <summary>
/// Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.
/// </summary>
internal sealed class CloseTermCommandValidator : AbstractValidator<CloseTermCommand>;
