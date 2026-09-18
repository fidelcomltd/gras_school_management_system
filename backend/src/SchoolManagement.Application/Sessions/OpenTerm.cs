using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Sessions;

/// <summary><c>POST /api/v1/terms/{id}/open</c> (spec 6.3.6). Moves upcoming to active.</summary>
/// <param name="Id">The term being opened.</param>
public sealed record OpenTermCommand(Guid Id) : ICommand<Result<TermDto>>;

/// <summary>
/// Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.
/// </summary>
internal sealed class OpenTermCommandValidator : AbstractValidator<OpenTermCommand>;
