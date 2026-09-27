using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.Assignments;

/// <summary>
/// <c>POST /assignments/copy-to-session</c> (spec 6.1.14, 4.2.2; TASK-0046 B): carries a session's active assignments into
/// another session. With <paramref name="DryRun"/> it only reports what it would create and skip, so the list can be
/// reviewed before anything is written.
/// </summary>
/// <param name="FromSessionId">The session whose active assignments are copied.</param>
/// <param name="ToSessionId">The session they are copied into; must not be Closed.</param>
/// <param name="DryRun">True to report without writing.</param>
public sealed record CopyAssignmentsToSessionCommand(string FromSessionId, string ToSessionId, bool DryRun)
    : ICommand<Result<AssignmentCopyResultDto>>;

/// <summary>What a copy did, or with a dry run would do.</summary>
/// <param name="DryRun">Whether this was only a preview.</param>
/// <param name="FromSessionName">The source session's name.</param>
/// <param name="ToSessionName">The target session's name.</param>
/// <param name="Copied">The assignments created (or that would be).</param>
/// <param name="Skipped">The assignments not copied, each with its reason.</param>
public sealed record AssignmentCopyResultDto(
    bool DryRun,
    string FromSessionName,
    string ToSessionName,
    IReadOnlyList<AssignmentCopyRowDto> Copied,
    IReadOnlyList<AssignmentCopyRowDto> Skipped);

/// <summary>One source assignment in a copy.</summary>
/// <param name="SourceAssignmentId">The assignment in the source session.</param>
/// <param name="AdminAccountId">The account holding it.</param>
/// <param name="StaffName">That account's staff name.</param>
/// <param name="RoleName">The role's name.</param>
/// <param name="ScopeType">School-wide or a list of classes.</param>
/// <param name="ArmNames">The classes, as named in the target session when copied, in the source session when skipped.</param>
/// <param name="SkipReason">Why it was not copied; <see langword="null"/> when it was.</param>
public sealed record AssignmentCopyRowDto(
    string SourceAssignmentId,
    string AdminAccountId,
    string StaffName,
    string RoleName,
    ScopeType ScopeType,
    IReadOnlyList<string> ArmNames,
    string? SkipReason);

/// <summary>Validates <see cref="CopyAssignmentsToSessionCommand"/>.</summary>
internal sealed class CopyAssignmentsToSessionCommandValidator : AbstractValidator<CopyAssignmentsToSessionCommand>
{
    public CopyAssignmentsToSessionCommandValidator()
    {
        RuleFor(command => command.FromSessionId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("fromSessionId must be a valid identifier.");

        RuleFor(command => command.ToSessionId)
            .Must(value => Guid.TryParse(value, out _))
            .WithMessage("toSessionId must be a valid identifier.");

        RuleFor(command => command.ToSessionId)
            .NotEqual(command => command.FromSessionId)
            .WithMessage("Choose a different session to copy into.");
    }
}
