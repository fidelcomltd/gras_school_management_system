using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>GET /api/v1/settings/reg-number/preview</c> (spec 6.2.4). <see cref="Separator"/> and
/// <see cref="SerialWidth"/> are UNSAVED — supplied as query parameters, never read from
/// <see cref="Domain.Settings.SchoolProfile"/>. The abbreviation and the currently saved
/// <c>serialReset</c> (which selects the counter partition — approved delta amendment 1) both come
/// from saved state.
/// </summary>
/// <param name="Separator">One of <c>/</c>, <c>-</c>, <c>.</c>. Not persisted by this query.</param>
/// <param name="SerialWidth">3 to 6. Not persisted by this query.</param>
public sealed record GetRegNumberPreviewQuery(string Separator, int SerialWidth)
    : IQuery<Result<RegNumberPreviewDto>>;

/// <summary>Validates <see cref="GetRegNumberPreviewQuery"/> against the same field rules as a real save.</summary>
internal sealed class GetRegNumberPreviewQueryValidator : AbstractValidator<GetRegNumberPreviewQuery>
{
    public GetRegNumberPreviewQueryValidator()
    {
        RuleFor(query => query.Separator)
            .Must(RegNumberFormat.IsValidSeparator)
            .WithMessage("Separator must be one of / - .");

        RuleFor(query => query.SerialWidth)
            .InclusiveBetween(RegNumberFormat.MinSerialWidth, RegNumberFormat.MaxSerialWidth);
    }
}

/// <summary>The response body of <see cref="GetRegNumberPreviewQuery"/>.</summary>
/// <param name="Preview">
/// The full registration number that would be issued right now under the supplied unsaved
/// parameters, for example <c>GRAS/2026/0041</c>. Uses the next serial that would actually be
/// issued — with an empty register that is serial 1.
/// </param>
public sealed record RegNumberPreviewDto(string Preview);
