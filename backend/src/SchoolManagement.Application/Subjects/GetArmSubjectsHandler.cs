using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="GetArmSubjectsQuery"/> by calling the sole resolver.</summary>
internal sealed class GetArmSubjectsHandler(SubjectsInEffectResolver resolver)
    : IRequestHandler<GetArmSubjectsQuery, Result<IReadOnlyList<ArmSubjectDto>>>
{
    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ArmSubjectDto>>> HandleAsync(
        GetArmSubjectsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolution = await resolver
            .ResolveAsync(Guid.Parse(request.ArmId), Guid.Parse(request.TermId), cancellationToken)
            .ConfigureAwait(false);

        if (resolution.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ArmSubjectDto>>(resolution.Error);
        }

        var dtos = resolution.Value
            .Select(row => new ArmSubjectDto(
                row.SubjectId.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
                row.SubjectName,
                row.SubjectCode,
                row.DisplayOrder,
                row.Source))
            .ToArray();

        return Result.Success<IReadOnlyList<ArmSubjectDto>>(dtos);
    }
}
