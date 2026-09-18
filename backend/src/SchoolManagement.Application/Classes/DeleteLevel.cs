using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>DELETE /api/v1/levels/{id}</c> (spec 6.4.2, 6.4.9): "Permitted only when nothing has ever
/// referenced the level." See <c>DeleteLevelHandler</c>'s remarks for exactly which references this
/// card can check today versus which are DEFERRED.
/// </summary>
/// <param name="Id">The level being removed.</param>
public sealed record DeleteLevelCommand(Guid Id) : ICommand;

/// <summary>Trivial but mandatory — the command carries no field beyond the route-bound <c>Id</c>.</summary>
internal sealed class DeleteLevelCommandValidator : AbstractValidator<DeleteLevelCommand>;
