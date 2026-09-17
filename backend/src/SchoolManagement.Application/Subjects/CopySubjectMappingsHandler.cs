using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="CopySubjectMappingsCommand"/>.</summary>
internal sealed class CopySubjectMappingsHandler(
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IClassLevelRepository levels,
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CopySubjectMappingsCommand, Result<SaveSubjectMappingGridResponse>>
{
    private const string EntityType = "subject_mapping";

    /// <inheritdoc />
    public async Task<Result<SaveSubjectMappingGridResponse>> HandleAsync(
        CopySubjectMappingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceTermId = Guid.Parse(request.SourceTermId);
        var destinationTermId = Guid.Parse(request.DestinationTermId);

        var sourceTerm = await terms.FindReadOnlyByIdAsync(sourceTermId, cancellationToken).ConfigureAwait(false);

        if (sourceTerm is null)
        {
            return Result.Failure<SaveSubjectMappingGridResponse>(Error.NotFound(
                "term.not_found", "No source term was found with that id."));
        }

        var destinationTerm = await terms.FindTrackedByIdAsync(destinationTermId, cancellationToken).ConfigureAwait(false);

        if (destinationTerm is null)
        {
            return Result.Failure<SaveSubjectMappingGridResponse>(Error.NotFound(
                "term.not_found", "No destination term was found with that id."));
        }

        if (destinationTerm.State == TermState.Closed)
        {
            var destinationSession = await sessions.FindReadOnlyByIdAsync(destinationTerm.SessionId, cancellationToken).ConfigureAwait(false);

            return Result.Failure<SaveSubjectMappingGridResponse>(Error.Conflict(
                "subject_mapping.term_closed",
                SubjectMappingMessages.TermClosed(destinationTerm.Name, destinationSession?.Name ?? string.Empty)));
        }

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var activeLevelIds = allLevels.Where(level => level.Status == LevelStatus.Active).Select(level => level.Id).ToHashSet();
        var levelsById = allLevels.ToDictionary(level => level.Id);

        var allSubjects = await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var activeSubjectIds = allSubjects.Where(subject => subject.Status == SubjectStatus.Active).Select(subject => subject.Id).ToHashSet();
        var subjectsById = allSubjects.ToDictionary(subject => subject.Id);

        var sourceMappings = await mappings.ListActiveByTermReadOnlyAsync(sourceTermId, cancellationToken).ConfigureAwait(false);

        // Only rows whose subject and level are STILL active are copied forward — a source mapping
        // whose subject or level has since been deactivated cannot become a NEW mapping (spec 6.6.3:
        // "must reference an active subject"/"active level" at creation), so it is silently omitted
        // rather than surfaced as an error the administrator did not ask about.
        var desired = sourceMappings
            .Where(mapping => activeSubjectIds.Contains(mapping.SubjectId) && activeLevelIds.Contains(mapping.ClassLevelId))
            .Select(mapping => new DesiredSubjectMappingEntry(mapping.SubjectId, mapping.ClassLevelId, mapping.DisplayOrder))
            .ToArray();

        var destinationCurrentActive = (await mappings.ListByTermTrackedAsync(destinationTermId, cancellationToken).ConfigureAwait(false))
            .Where(mapping => mapping.Status == SubjectMappingStatus.Active)
            .ToArray();

        var diff = SubjectMappingGridDiff.Compute(destinationCurrentActive, desired, additiveOnly: true);

        var additionDtos = diff.Additions
            .Select(entry => new SubjectMappingChangeDto(
                entry.SubjectId.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
                subjectsById.TryGetValue(entry.SubjectId, out var subject) ? subject.Name : "Unknown subject",
                entry.ClassLevelId.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
                levelsById.TryGetValue(entry.ClassLevelId, out var level) ? level.Name : "Unknown level"))
            .ToArray();

        if (request.DryRun)
        {
            return Result.Success(new SaveSubjectMappingGridResponse(additionDtos, Endings: [], DryRun: true));
        }

        foreach (var addition in diff.Additions)
        {
            var creation = SubjectMapping.Create(
                Guid.CreateVersion7(), addition.SubjectId, addition.ClassLevelId, destinationTerm.SessionId, destinationTermId, addition.DisplayOrder);

            if (creation.IsFailure)
            {
                return Result.Failure<SaveSubjectMappingGridResponse>(creation.Error);
            }

            await mappings.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
        }

        await auditSink.RecordAsync(
            Privileges.Subject.Map,
            EntityType,
            entityId: null,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sourceTermId"] = request.SourceTermId,
                ["destinationTermId"] = request.DestinationTermId,
                ["additionCount"] = additionDtos.Length,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new SaveSubjectMappingGridResponse(additionDtos, Endings: [], DryRun: false));
    }
}
