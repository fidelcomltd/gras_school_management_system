using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// <c>PATCH /api/v1/sessions/{id}</c> (spec 6.3.10): "Name and dates, while upcoming or active."
/// Every field is independently optional; an absent field is left unchanged — the same convention
/// <c>UpdateRoleCommand</c> established.
/// </summary>
/// <param name="Id">The session being edited.</param>
/// <param name="Name"><see langword="null"/> to leave unchanged.</param>
/// <param name="StartDate"><see langword="null"/> to leave unchanged.</param>
/// <param name="EndDate"><see langword="null"/> to leave unchanged.</param>
public sealed record UpdateSessionCommand(Guid Id, string? Name, DateOnly? StartDate, DateOnly? EndDate)
    : ICommand<Result<SessionDetailDto>>;

/// <summary>Validates <see cref="UpdateSessionCommand"/>.</summary>
internal sealed class UpdateSessionCommandValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionCommandValidator()
    {
        RuleFor(command => command.Name)
            .Length(AcademicSession.NameLength)
            .When(command => command.Name is not null);
    }
}
