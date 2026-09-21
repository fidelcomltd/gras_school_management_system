using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
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
/// Handles <see cref="PrefillSubjectMappingsCommand"/>.
/// </summary>
/// <remarks>
/// Resolves the standard list by section rather than by <see cref="SubjectSheet"/> directly: a
/// Nursery-section level receives every subject named in <see cref="SeededSubjects.NurserySheetOrder"/>
/// at that list's position, a Primary-section level every subject named in
/// <see cref="SeededSubjects.PrimarySheetOrder"/> at ITS position — the five shared subjects simply
/// appear, by name, in both lists, so no separate "Both" branch is needed. Only the two ORIGINALLY
/// SEEDED sections (<see cref="SeededClassLevels.NurserySectionId"/>/<see cref="SeededClassLevels.PrimarySectionId"/>)
/// are recognised, matching spec's own framing ("the three Nursery levels"/"the six Primary levels");
/// a level under any other section is left untouched by this action.
/// </remarks>
internal sealed class PrefillSubjectMappingsHandler(
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IClassLevelRepository levels,
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<PrefillSubjectMappingsCommand, Result<SaveSubjectMappingGridResponse>>
{
    private const string EntityType = "subject_mapping";

    /// <inheritdoc />
    public async Task<Result<SaveSubjectMappingGridResponse>> HandleAsync(
        PrefillSubjectMappingsCommand request, CancellationToken cancellationToken)
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

        var activeNurseryLevels = allLevels
            .Where(level => level.Status == LevelStatus.Active && level.SectionId == SeededClassLevels.NurserySectionId)
            .ToArray();

        var activePrimaryLevels = allLevels
            .Where(level => level.Status == LevelStatus.Active && level.SectionId == SeededClassLevels.PrimarySectionId)
            .ToArray();

        var allSubjects = await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var activeSubjectsByName = allSubjects
            .Where(subject => subject.Status == SubjectStatus.Active)
            .ToDictionary(subject => subject.Name, StringComparer.Ordinal);

        var desired = new List<DesiredSubjectMappingEntry>();

        foreach (var level in activeNurseryLevels)
        {
            AppendSheetEntries(desired, level.Id, SeededSubjects.NurserySheetOrder, activeSubjectsByName);
        }

        foreach (var level in activePrimaryLevels)
        {
            AppendSheetEntries(desired, level.Id, SeededSubjects.PrimarySheetOrder, activeSubjectsByName);
        }

        var currentActive = (await mappings.ListByTermTrackedAsync(termId, cancellationToken).ConfigureAwait(false))
            .Where(mapping => mapping.Status == SubjectMappingStatus.Active)
            .ToArray();

        var diff = SubjectMappingGridDiff.Compute(currentActive, desired, additiveOnly: true);

        var subjectsById = allSubjects.ToDictionary(subject => subject.Id);

        var additionDtos = diff.Additions
            .Select(entry => new SubjectMappingChangeDto(
                entry.SubjectId.ToString("D", CultureInfo.InvariantCulture),
                subjectsById.TryGetValue(entry.SubjectId, out var subject) ? subject.Name : "Unknown subject",
                entry.ClassLevelId.ToString("D", CultureInfo.InvariantCulture),
                levelsById.TryGetValue(entry.ClassLevelId, out var level) ? level.Name : "Unknown level"))
            .ToArray();

        if (request.DryRun)
        {
            return Result.Success(new SaveSubjectMappingGridResponse(additionDtos, Endings: [], DryRun: true));
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

        await auditSink.RecordAsync(
            Privileges.Subject.Map,
            EntityType,
            entityId: null,
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["termId"] = request.TermId,
                ["additionCount"] = additionDtos.Length,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        // TASK-0088 AC A2: prefill is additive-only, so every affected class level came from an
        // addition — flags every non-Published result set of this term for those class levels.
        var affectedClassLevelIds = diff.Additions.Select(addition => addition.ClassLevelId).ToHashSet();

        if (affectedClassLevelIds.Count > 0)
        {
            var lockedResultSets = await resultSets
                .LockNonPublishedByTermAndClassLevelsAsync(termId, affectedClassLevelIds, cancellationToken)
                .ConfigureAwait(false);

            await ResultSetRecomputeFlagger.FlagAsync(lockedResultSets, auditSink, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new SaveSubjectMappingGridResponse(additionDtos, Endings: [], DryRun: false));
    }

    private static void AppendSheetEntries(
        List<DesiredSubjectMappingEntry> desired,
        Guid classLevelId,
        IReadOnlyList<string> sheetOrder,
        Dictionary<string, Subject> activeSubjectsByName)
    {
        for (var index = 0; index < sheetOrder.Count; index++)
        {
            if (activeSubjectsByName.TryGetValue(sheetOrder[index], out var subject))
            {
                desired.Add(new DesiredSubjectMappingEntry(subject.Id, classLevelId, index + 1));
            }
        }
    }
}
