using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// <c>GET /api/v1/sections</c> (spec 6.4.9). Not paged — see <see cref="SectionListResponse"/>'s remarks.
/// Carries no field: the whole list is always returned.
/// </summary>
public sealed record ListSectionsQuery : IQuery<Result<SectionListResponse>>;

/// <summary>Trivial but mandatory — the query carries no field at all.</summary>
internal sealed class ListSectionsQueryValidator : AbstractValidator<ListSectionsQuery>;
