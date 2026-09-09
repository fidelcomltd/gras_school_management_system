using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="FindPupilDuplicatesQuery"/>.</summary>
internal sealed class FindPupilDuplicatesQueryHandler(IPupilRepository pupils, TimeProvider timeProvider)
    : IRequestHandler<FindPupilDuplicatesQuery, Result<IReadOnlyList<PupilDto>>>
{
    /// <summary>A candidates panel, not a paged list (spec 6.5.11).</summary>
    private const int MaxResults = 20;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PupilDto>>> HandleAsync(
        FindPupilDuplicatesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var candidates = await pupils
            .FindDuplicatesAsync(request.Surname, request.FirstName, request.DateOfBirth, MaxResults, today, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(candidates);
    }
}
