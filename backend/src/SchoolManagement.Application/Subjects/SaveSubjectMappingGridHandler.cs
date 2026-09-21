using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// Handles <see cref="SaveSubjectMappingGridCommand"/>.
/// </summary>
/// <remarks>
/// TASK-0070 delta amendment 2: the route requires only <c>subject.view</c> as a reachability
/// baseline (a caller must be able to see the grid to save it) — <c>subject.map</c> and
/// <c>subject.unmap</c> are both DATA-DEPENDENT, checked here against the computed diff, the same
/// shape <c>UpdateArmHandler</c> uses for <c>arm.formteacher.assign</c>. Evaluated identically whether
/// or not <see cref="SaveSubjectMappingGridCommand.DryRun"/> is set, so a caller cannot discover the
/// ending set without holding the privilege to perform it.
/// </remarks>
internal sealed class SaveSubjectMappingGridHandler(
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IClassLevelRepository levels,
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ISubjectMappingMarkLookup markLookup,
    IEffectivePrivilegeProvider effectivePrivilegeProvider,
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveSubjectMappingGridCommand, Result<SaveSubjectMappingGridResponse>>
{
    private const string EntityType = "subject_mapping";

    /// <inheritdoc />
    public async Task<Result<SaveSubjectMappingGridResponse>> HandleAsync(
        SaveSubjectMappingGridCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var termId = Guid.Parse(request.TermId);
        var term = await terms.FindTrackedByIdAsync(termId, cancellationToken).ConfigureAwait(false);

        if (term is null)
        {
            return Result.Failure<SaveSubjectMappingGridResponse>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.State == TermState.Closed)
        {
            var session = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);

            return Result.Failure<SaveSubjectMappingGridResponse>(Error.Conflict(
                "subject_mapping.term_closed", SubjectMappingMessages.TermClosed(term.Name, session?.Name ?? string.Empty)));
        }

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelsById = allLevels.ToDictionary(level => level.Id);
        var allSubjects = await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var subjectsById = allSubjects.ToDictionary(subject => subject.Id);

        var desired = new List<DesiredSubjectMappingEntry>();

        foreach (var entry in request.Entries)
        {
            var subjectId = Guid.Parse(entry.SubjectId);
            var classLevelId = Guid.Parse(entry.ClassLevelId);

            if (!subjectsById.TryGetValue(subjectId, out var subject) || subject.Status != SubjectStatus.Active)
            {
                return Result.Failure<SaveSubjectMappingGridResponse>(Error.Validation(
                    "subject_mapping.subject_not_found", "No active subject was found with that id."));
            }

            if (!levelsById.TryGetValue(classLevelId, out var level) || level.Status != LevelStatus.Active)
            {
                return Result.Failure<SaveSubjectMappingGridResponse>(Error.Validation(
                    "subject_mapping.level_not_found", "No active class level was found with that id."));
            }

            desired.Add(new DesiredSubjectMappingEntry(subjectId, classLevelId, entry.DisplayOrder));
        }

        var currentActive = (await mappings.ListByTermTrackedAsync(termId, cancellationToken).ConfigureAwait(false))
            .Where(mapping => mapping.Status == SubjectMappingStatus.Active)
            .ToArray();

        var diff = SubjectMappingGridDiff.Compute(currentActive, desired, additiveOnly: false);

        var privilegeCheck = await CheckPrivilegesAsync(diff, cancellationToken).ConfigureAwait(false);

        if (privilegeCheck.IsFailure)
        {
            return Result.Failure<SaveSubjectMappingGridResponse>(privilegeCheck.Error);
        }

        foreach (var ending in diff.Endings)
        {
            var marks = await markLookup
                .FindArmsWithMarksAsync(ending.SubjectId, ending.ClassLevelId, termId, cancellationToken)
                .ConfigureAwait(false);

            if (marks.Count > 0)
            {
                var subjectName = subjectsById.TryGetValue(ending.SubjectId, out var subject) ? subject.Name : "This subject";

                return Result.Failure<SaveSubjectMappingGridResponse>(Error.Conflict(
                    "subject_mapping.marks_recorded", SubjectMappingMessages.MarksRecorded(subjectName, marks)));
            }
        }

        var additionDtos = diff.Additions
            .Select(entry => ToChangeDto(entry.SubjectId, entry.ClassLevelId, subjectsById, levelsById))
            .ToArray();

        var endingDtos = diff.Endings
            .Select(mapping => ToChangeDto(mapping.SubjectId, mapping.ClassLevelId, subjectsById, levelsById))
            .ToArray();

        if (request.DryRun)
        {
            return Result.Success(new SaveSubjectMappingGridResponse(additionDtos, endingDtos, DryRun: true));
        }

        foreach (var addition in diff.Additions)
        {
            var creation = SubjectMapping.Create(
                Guid.CreateVersion7(), addition.SubjectId, addition.ClassLevelId, term.SessionId, termId, addition.DisplayOrder);

            if (creation.IsFailure)
            {
                return Result.Failure<SaveSubjectMappingGridResponse>(creation.Error);
            }

            await mappings.AddAsync(creation.Value, cancellationToken).ConfigureAwait(false);
        }

        foreach (var ending in diff.Endings)
        {
            ending.End();
        }

        foreach (var (mapping, newDisplayOrder) in diff.Reorders)
        {
            mapping.ChangeDisplayOrder(newDisplayOrder);
        }

        await auditSink.RecordAsync(
            Privileges.Subject.Map,
            EntityType,
            entityId: null,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["termId"] = request.TermId,
                ["additionCount"] = additionDtos.Length,
                ["endingCount"] = endingDtos.Length,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        // TASK-0088 AC A2: flags every non-Published result set of this term whose arm's in-effect
        // subject list this save can change — every class level an addition or an ending touched.
        // Reorders never change what is in effect, so they never widen this set.
        var affectedClassLevelIds = diff.Additions.Select(addition => addition.ClassLevelId)
            .Concat(diff.Endings.Select(ending => ending.ClassLevelId))
            .ToHashSet();

        if (affectedClassLevelIds.Count > 0)
        {
            var lockedResultSets = await resultSets
                .LockNonPublishedByTermAndClassLevelsAsync(termId, affectedClassLevelIds, cancellationToken)
                .ConfigureAwait(false);

            await ResultSetRecomputeFlagger.FlagAsync(lockedResultSets, auditSink, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new SaveSubjectMappingGridResponse(additionDtos, endingDtos, DryRun: false));
    }

    private async Task<Result> CheckPrivilegesAsync(SubjectMappingGridDiffResult diff, CancellationToken cancellationToken)
    {
        if (diff.Additions.Count == 0 && diff.Endings.Count == 0)
        {
            return Result.Success();
        }

        if (currentUser.UserId is not { } actorId)
        {
            return Result.Failure(Error.Unauthenticated("authentication.required", "Sign in to perform this action."));
        }

        var grants = await effectivePrivilegeProvider.GetGrantsAsync(actorId, cancellationToken).ConfigureAwait(false);

        if (diff.Additions.Count > 0 && !grants.Any(grant => grant.Privilege == Privileges.Subject.Map))
        {
            return Result.Failure(Error.Forbidden(
                "subject_mapping.map_denied", "You do not have permission to map a subject to a level."));
        }

        if (diff.Endings.Count > 0 && !grants.Any(grant => grant.Privilege == Privileges.Subject.Unmap))
        {
            return Result.Failure(Error.Forbidden(
                "subject_mapping.unmap_denied", "You do not have permission to end a subject mapping."));
        }

        return Result.Success();
    }

    private static SubjectMappingChangeDto ToChangeDto(
        Guid subjectId, Guid classLevelId, Dictionary<Guid, Subject> subjectsById, Dictionary<Guid, ClassLevel> levelsById)
    {
        var subjectName = subjectsById.TryGetValue(subjectId, out var subject) ? subject.Name : "Unknown subject";
        var levelName = levelsById.TryGetValue(classLevelId, out var level) ? level.Name : "Unknown level";

        return new SubjectMappingChangeDto(
            subjectId.ToString("D", CultureInfo.InvariantCulture),
            subjectName,
            classLevelId.ToString("D", CultureInfo.InvariantCulture),
            levelName);
    }
}
