using System.Globalization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="ListSectionsQuery"/>.</summary>
internal sealed class ListSectionsHandler(ISectionRepository sections)
    : IRequestHandler<ListSectionsQuery, Result<SectionListResponse>>
{
    /// <inheritdoc />
    public async Task<Result<SectionListResponse>> HandleAsync(ListSectionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var all = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        var items = all
            .OrderBy(section => section.Name, StringComparer.Ordinal)
            .Select(section => new SectionDto(section.Id.ToString("D", CultureInfo.InvariantCulture), section.Name))
            .ToArray();

        return Result.Success(new SectionListResponse(items));
    }
}
