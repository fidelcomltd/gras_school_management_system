using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="GetLevelQuery"/>.</summary>
internal sealed class GetLevelHandler(IClassLevelRepository levels, ISectionRepository sections)
    : IRequestHandler<GetLevelQuery, Result<LevelDto>>
{
    /// <inheritdoc />
    public async Task<Result<LevelDto>> HandleAsync(GetLevelQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = allLevels.FirstOrDefault(candidate => candidate.Id == request.Id);

        if (level is null)
        {
            return Result.Failure<LevelDto>(Error.NotFound("level.not_found", "No class level was found with that id."));
        }

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var sectionNamesById = allSections.ToDictionary(section => section.Id, section => section.Name);

        return Result.Success(LevelMapper.ToDto(level, allLevels, sectionNamesById));
    }
}
