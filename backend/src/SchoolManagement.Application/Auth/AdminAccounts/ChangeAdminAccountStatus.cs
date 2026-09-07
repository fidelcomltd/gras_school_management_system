using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Auth.AdminAccounts;

/// <summary>
/// <c>POST /api/v1/admins/{id}/status</c> (spec 6.1.10, 6.1.14). The privilege required is
/// DATA-DEPENDENT on <paramref name="Status"/> — <c>admin.suspend</c> for the active/suspended pair,
/// <c>admin.deactivate</c> for deactivation, and <c>admin.deactivate</c> HELD BY A SUPER ADMIN for
/// reactivating a deactivated account — so this route is mapped with <c>RequireAuthenticatedCaller()</c>
/// and the handler resolves the exact requirement.
/// </summary>
/// <param name="Id">The account whose status is changing.</param>
/// <param name="Status">The target status.</param>
/// <param name="Reason">
/// Required, at least ten characters, when <paramref name="Status"/> is
/// <see cref="AdminAccountStatus.Deactivated"/> (spec 6.1.12: "A reason is mandatory on:...
/// admin deactivation").
/// </param>
public sealed record ChangeAdminAccountStatusCommand(Guid Id, AdminAccountStatus Status, string? Reason)
    : ICommand<Result<AdminAccountDetailDto>>;

/// <summary>Validates <see cref="ChangeAdminAccountStatusCommand"/>.</summary>
internal sealed class ChangeAdminAccountStatusCommandValidator
    : AbstractValidator<ChangeAdminAccountStatusCommand>
{
    /// <summary>Spec 6.1.12: "shorter than ten characters" is rejected.</summary>
    private const int ReasonMinLength = 10;

    private const int ReasonMaxLength = 500;

    public ChangeAdminAccountStatusCommandValidator()
    {
        RuleFor(command => command.Status).IsInEnum();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .WithMessage("A reason is required for this change.")
            .MinimumLength(ReasonMinLength)
            .WithMessage("A reason is required for this change.")
            .MaximumLength(ReasonMaxLength)
            .When(command => command.Status == AdminAccountStatus.Deactivated);
    }
}
