using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reference.SampleRecords;

/// <summary>
/// REFERENCE SLICE — the minimal PAGINATED QUERY. Copy this shape for any collection endpoint.
/// </summary>
/// <remarks>
/// There is no unpaginated variant, and there must never be one. An endpoint that returns "all"
/// rows works fine on a developer machine with fifty rows and takes the service down at fifty
/// thousand.
/// </remarks>
/// <param name="Page">1-based page number. Defaults to the first page when omitted.</param>
/// <param name="PageSize">
/// Items per page. Defaults to <see cref="PageRequest.DefaultPageSize"/>, capped at
/// <see cref="PageRequest.MaxPageSize"/>.
/// </param>
public sealed record ListSampleRecordsQuery(int? Page, int? PageSize)
    : IQuery<Result<PagedResult<SampleRecordDto>>>;

/// <summary>
/// Validates <see cref="ListSampleRecordsQuery"/>.
/// </summary>
/// <remarks>
/// Page size above the maximum is REJECTED rather than silently clamped. Clamping would answer a
/// request for 500 items with 100 and no indication, and a client paging through the results would
/// then skip four fifths of them while believing it had read everything. A 422 tells it plainly.
/// </remarks>
internal sealed class ListSampleRecordsQueryValidator : AbstractValidator<ListSampleRecordsQuery>
{
    /// <summary>Configures the rules.</summary>
    public ListSampleRecordsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(PageRequest.FirstPage)
            .WithMessage($"Page must be {PageRequest.FirstPage} or greater.")
            .When(query => query.Page is not null);

        RuleFor(query => query.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be 1 or greater.")
            .LessThanOrEqualTo(PageRequest.MaxPageSize)
            .WithMessage($"PageSize must be at most {PageRequest.MaxPageSize}.")
            .When(query => query.PageSize is not null);
    }
}
