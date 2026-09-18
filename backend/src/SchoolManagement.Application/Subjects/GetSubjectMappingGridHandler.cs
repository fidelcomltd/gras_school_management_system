using System.Globalization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="GetSubjectMappingGridQuery"/>.</summary>
internal sealed class GetSubjectMappingGridHandler(
    ITermRepository terms,
    IClassLevelRepository levels,
    IArmRepository arms,
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ISubjectMappingExceptionRepository exceptions)
    : IRequestHandler<GetSubjectMappingGridQuery, Result<SubjectMappingGridDto>>
{
    /// <inheritdoc />
    public async Task<Result<SubjectMappingGridDto>> HandleAsync(
        GetSubjectMappingGridQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var termId = Guid.Parse(request.TermId);
        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<SubjectMappingGridDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelNamesById = allLevels.ToDictionary(level => level.Id, level => level.Name);
        var activeLevels = allLevels
            .Where(level => level.Status == LevelStatus.Active)
            .OrderBy(level => level.ProgressionOrder)
            .ToArray();

        var activeMappings = await mappings.ListActiveByTermReadOnlyAsync(termId, cancellationToken).ConfigureAwait(false);
        var mappingByPair = activeMappings.ToDictionary(mapping => (mapping.SubjectId, mapping.ClassLevelId));
        var mappedSubjectIds = activeMappings.Select(mapping => mapping.SubjectId).ToHashSet();

        var allSubjects = await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var subjectRows = allSubjects
            .Where(subject => subject.Status == SubjectStatus.Active || mappedSubjectIds.Contains(subject.Id))
            .OrderBy(subject => subject.NameKey, StringComparer.Ordinal)
            .Select(subject => new SubjectMappingGridSubjectRowDto(
                subject.Id.ToString("D", CultureInfo.InvariantCulture),
                subject.Name,
                subject.Code,
                activeLevels
                    .Select(level => mappingByPair.TryGetValue((subject.Id, level.Id), out var mapping)
                        ? new SubjectMappingGridCellDto(level.Id.ToString("D", CultureInfo.InvariantCulture), true, mapping.DisplayOrder)
                        : new SubjectMappingGridCellDto(level.Id.ToString("D", CultureInfo.InvariantCulture), false, null))
                    .ToArray()))
            .ToArray();

        var levelDtos = activeLevels
            .Select(level => new SubjectMappingGridLevelDto(
                level.Id.ToString("D", CultureInfo.InvariantCulture), level.Name, level.ProgressionOrder))
            .ToArray();

        var exceptionsForTerm = await exceptions.ListByTermReadOnlyAsync(termId, cancellationToken).ConfigureAwait(false);
        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var armsById = allArms.ToDictionary(arm => arm.Id);

        var armSummaries = exceptionsForTerm
            .GroupBy(exception => exception.ArmId)
            .Where(group => armsById.ContainsKey(group.Key))
            .Select(group =>
            {
                var arm = armsById[group.Key];
                var levelName = levelNamesById.TryGetValue(arm.ClassLevelId, out var name) ? name : "Unknown level";

                return new SubjectMappingGridArmExceptionSummaryDto(
                    arm.Id.ToString("D", CultureInfo.InvariantCulture),
                    ArmDisplayName.Compose(levelName, arm.Label),
                    group.Count(exception => exception.Mode == SubjectExceptionMode.Include),
                    group.Count(exception => exception.Mode == SubjectExceptionMode.Exclude));
            })
            .ToArray();

        return Result.Success(new SubjectMappingGridDto(levelDtos, subjectRows, armSummaries));
    }
}
