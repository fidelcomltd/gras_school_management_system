using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Reads the school settings (spec 6.2.12: "Everything in one payload for the settings area").
/// Returns only the <c>identity</c> group as of TASK-0005a; TASK-0005b/0005c extend the same
/// <see cref="SettingsDto"/> additively.
/// </summary>
public sealed record GetSettingsQuery : IQuery<Result<SettingsDto>>;

/// <summary>Validates <see cref="GetSettingsQuery"/>. Empty — the query takes no parameters; a validator still exists so <c>ValidatorCoverageTests</c> records that the question was considered.</summary>
internal sealed class GetSettingsQueryValidator : AbstractValidator<GetSettingsQuery>;
