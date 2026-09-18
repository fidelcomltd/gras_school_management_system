using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Reads the result rules (spec 6.2.8) — <c>GET /api/v1/settings/result-rules</c>. A dedicated
/// endpoint, not folded into <see cref="GetSettingsQuery"/>'s envelope (TASK-0077's approved delta:
/// "two operations on one new path").
/// </summary>
public sealed record GetResultRulesQuery : IQuery<Result<ResultRulesDto>>;

/// <summary>Validates <see cref="GetResultRulesQuery"/>. Empty — the query takes no parameters; a validator still exists so <c>ValidatorCoverageTests</c> records that the question was considered.</summary>
internal sealed class GetResultRulesQueryValidator : AbstractValidator<GetResultRulesQuery>;
