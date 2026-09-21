using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>POST /api/v1/result-sets/{resultSetId}/return</c> (spec 6.7.8, 6.7.11; TASK-0090's approved
/// contract delta). Moves Awaiting Approval or Approved to Returned for Correction.
/// </summary>
/// <param name="ResultSetId">The result set to return, from the route.</param>
/// <param name="Reason">
/// Trimmed to between <see cref="ResultSet.ReturnReasonMinLength"/> and
/// <see cref="ResultSet.ReturnReasonMaxLength"/> characters, else 422 (spec 6.7.8, 6.7.3).
/// </param>
public sealed record ReturnResultSetCommand(Guid ResultSetId, string Reason) : ICommand<Result<ReturnResultSetResponse>>;

/// <summary>
/// Structural check only — the bound is on the TRIMMED reason (spec 6.7.8's "reason of at least ten
/// characters" is not satisfied by ten characters of leading whitespace), so this validates the
/// trimmed length rather than <see cref="AbstractValidator{T}.RuleFor{TProperty}"/>'s raw string.
/// </summary>
internal sealed class ReturnResultSetCommandValidator : AbstractValidator<ReturnResultSetCommand>
{
    public ReturnResultSetCommandValidator()
    {
        RuleFor(command => command.Reason)
            .NotNull()
            .Must(reason => reason.Trim().Length is >= ResultSet.ReturnReasonMinLength and <= ResultSet.ReturnReasonMaxLength)
            .WithMessage(
                $"Reason must be between {ResultSet.ReturnReasonMinLength} and " +
                $"{ResultSet.ReturnReasonMaxLength} characters.");
    }
}

/// <summary>The 200 response (contract delta item 2).</summary>
/// <param name="ResultSet">The set's new state (Returned for Correction) and its other summary fields.</param>
public sealed record ReturnResultSetResponse(ResultSetSummaryDto ResultSet);
