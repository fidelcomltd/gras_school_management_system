using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reference.SampleRecords;

/// <summary>
/// Handles <see cref="ListSampleRecordsQuery"/>.
/// </summary>
/// <remarks>
/// A query, so it gets NO transaction — the unit-of-work behaviour cannot be closed over a type that
/// does not implement <see cref="IBaseCommand"/>. The repository serves it with a no-tracking,
/// projected query.
/// </remarks>
internal sealed class ListSampleRecordsHandler(ISampleRecordRepository repository)
    : IRequestHandler<ListSampleRecordsQuery, Result<PagedResult<SampleRecordDto>>>
{
    /// <inheritdoc />
    public async Task<Result<PagedResult<SampleRecordDto>>> HandleAsync(
        ListSampleRecordsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = PageRequest.From(request.Page, request.PageSize);
        var records = await repository.ListAsync(page, cancellationToken);

        // An empty page is a successful result, not a 404. "No rows matched" is a valid answer to a
        // collection query; 404 is for a named resource that does not exist.
        return Result.Success(records);
    }
}
