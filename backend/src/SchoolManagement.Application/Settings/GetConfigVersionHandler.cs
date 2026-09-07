using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="GetConfigVersionQuery"/>.</summary>
internal sealed class GetConfigVersionQueryHandler(IConfigVersionRepository repository)
    : IRequestHandler<GetConfigVersionQuery, Result<ConfigVersionDetailDto>>
{
    /// <inheritdoc />
    public async Task<Result<ConfigVersionDetailDto>> HandleAsync(
        GetConfigVersionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await repository
            .FindReadOnlyByIdAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        return detail is null
            ? Result.Failure<ConfigVersionDetailDto>(Error.NotFound(
                "config_version.not_found",
                "No configuration version exists with that id."))
            : Result.Success(detail);
    }
}
