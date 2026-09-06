using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// <c>GET /api/v1/config-versions/{id}</c> — one version's full detail, including its whole
/// serialised configuration snapshot (spec 6.2.9).
/// </summary>
/// <param name="Id">The version's opaque id.</param>
public sealed record GetConfigVersionQuery(Guid Id) : IQuery<Result<ConfigVersionDetailDto>>;

/// <summary>Validates <see cref="GetConfigVersionQuery"/>.</summary>
internal sealed class GetConfigVersionQueryValidator : AbstractValidator<GetConfigVersionQuery>
{
    public GetConfigVersionQueryValidator() =>
        RuleFor(query => query.Id).NotEqual(Guid.Empty);
}
