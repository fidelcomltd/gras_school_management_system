using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// <c>GET /api/v1/geography/states</c>: the curated states and their LGAs that a pupil's state of origin and LGA must come
/// from (spec 6.5.4), so a form offers a choice instead of free text the server would refuse.
/// </summary>
public sealed record GetNigerianGeographyQuery : IQuery<Result<NigerianGeographyDto>>;

/// <summary>Nothing to validate.</summary>
internal sealed class GetNigerianGeographyQueryValidator : AbstractValidator<GetNigerianGeographyQuery>;

/// <summary>Handles <see cref="GetNigerianGeographyQuery"/> from <see cref="NigerianGeography"/>, the list the server validates against.</summary>
internal sealed class GetNigerianGeographyHandler : IRequestHandler<GetNigerianGeographyQuery, Result<NigerianGeographyDto>>
{
    private static readonly NigerianGeographyDto Geography = new(
        [.. NigerianGeography.States.Select(state => new NigerianStateDto(state, NigerianGeography.LgasOf(state)))]);

    /// <inheritdoc />
    public Task<Result<NigerianGeographyDto>> HandleAsync(GetNigerianGeographyQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(Geography));
}

/// <summary>The 36 states and the FCT, each with its LGAs.</summary>
/// <param name="States">In the National Assembly's order, as the server lists them.</param>
public sealed record NigerianGeographyDto(IReadOnlyList<NigerianStateDto> States);

/// <summary>One state and its local government areas.</summary>
/// <param name="Name">The canonical spelling the server stores.</param>
/// <param name="Lgas">Its LGAs, canonical spellings, in list order.</param>
public sealed record NigerianStateDto(string Name, IReadOnlyList<string> Lgas);
