using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="GetResultRulesQuery"/>.</summary>
internal sealed class GetResultRulesQueryHandler(
    IResultRulesRepository resultRulesRepository,
    ISchoolProfileRepository schoolProfileRepository)
    : IRequestHandler<GetResultRulesQuery, Result<ResultRulesDto>>
{
    /// <inheritdoc />
    public async Task<Result<ResultRulesDto>> HandleAsync(GetResultRulesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resultRules = await resultRulesRepository
            .GetReadOnlySingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        var profile = await schoolProfileRepository
            .GetReadOnlySingletonAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(SettingsMapper.ToResultRulesDto(resultRules, profile.ResultRulesVersionNumber));
    }
}
