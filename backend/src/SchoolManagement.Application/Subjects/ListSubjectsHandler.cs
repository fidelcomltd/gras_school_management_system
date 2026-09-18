using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="ListSubjectsQuery"/>.</summary>
internal sealed class ListSubjectsHandler(
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ISubjectMappingExceptionRepository exceptions,
    IArmRepository arms,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    SubjectsInEffectResolver resolver)
    : IRequestHandler<ListSubjectsQuery, Result<CursorPage<SubjectDto>>>
{
    /// <inheritdoc />
    public async Task<Result<CursorPage<SubjectDto>>> HandleAsync(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Cursor is not null && !SubjectListCursor.TryDecode(request.Cursor, out _, out _))
        {
            return Result.Failure<CursorPage<SubjectDto>>(Error.Validation(
                "subject.invalid_cursor", "The cursor is invalid or has expired. Start again from the first page."));
        }

        Guid? termId = null;

        if (request.TermId is not null)
        {
            var parsedTermId = Guid.Parse(request.TermId);
            var term = await terms.FindReadOnlyByIdAsync(parsedTermId, cancellationToken).ConfigureAwait(false);

            if (term is null)
            {
                return Result.Failure<CursorPage<SubjectDto>>(Error.Validation(
                    "subject.term_not_found", "No term was found with that id."));
            }

            termId = parsedTermId;
        }

        var pageSize = Math.Clamp(request.PageSize ?? CursorPageRequest.DefaultPageSize, 1, CursorPageRequest.MaxPageSize);

        var allSubjects = await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        var filtered = allSubjects
            .Where(subject => request.Status is null || subject.Status == request.Status)
            .ToArray();

        if (request.LevelId is not null)
        {
            var levelId = Guid.Parse(request.LevelId);

            IReadOnlyCollection<Guid> mappedSubjectIds = termId is { } scopedTerm
                ? (await mappings.ListActiveByLevelAndTermReadOnlyAsync(levelId, scopedTerm, cancellationToken).ConfigureAwait(false))
                    .Select(mapping => mapping.SubjectId)
                    .ToHashSet()
                : (await mappings.ListSubjectIdsEverMappedToLevelAsync(levelId, cancellationToken).ConfigureAwait(false))
                    .ToHashSet();

            filtered = filtered.Where(subject => mappedSubjectIds.Contains(subject.Id)).ToArray();
        }

        var ordered = filtered
            .OrderBy(subject => subject.NameKey, StringComparer.Ordinal)
            .ThenBy(subject => subject.Id)
            .ToArray();

        var hasCursor = SubjectListCursor.TryDecode(request.Cursor, out var cursorNameKey, out var cursorId);

        var candidates = ordered
            .Where(subject => !hasCursor || IsAfterCursor(subject.NameKey, subject.Id, cursorNameKey, cursorId))
            .ToArray();

        var page = candidates.Take(pageSize).ToArray();
        var hasNextPage = candidates.Length > pageSize;

        // Term-scoped counts (delta amendment 3: null whenever no termId was given) computed ONCE per
        // request over the term's own data, never per subject — same "admin-configuration-sized, load
        // the full set" reasoning as IArmRepository/IClassLevelRepository.
        IReadOnlyDictionary<Guid, int> mappedLevelCountBySubject = new Dictionary<Guid, int>();
        IReadOnlyDictionary<Guid, int> armExceptionCountBySubject = new Dictionary<Guid, int>();
        IReadOnlyDictionary<Guid, int> pupilsTakingCountBySubject = new Dictionary<Guid, int>();

        if (termId is { } term1)
        {
            var activeMappingsForTerm = await mappings.ListActiveByTermReadOnlyAsync(term1, cancellationToken).ConfigureAwait(false);

            mappedLevelCountBySubject = activeMappingsForTerm
                .GroupBy(mapping => mapping.SubjectId)
                .ToDictionary(group => group.Key, group => group.Select(mapping => mapping.ClassLevelId).Distinct().Count());

            var exceptionsForTerm = await exceptions.ListByTermReadOnlyAsync(term1, cancellationToken).ConfigureAwait(false);

            armExceptionCountBySubject = exceptionsForTerm
                .GroupBy(exception => exception.SubjectId)
                .ToDictionary(group => group.Key, group => group.Count());

            var term = await terms.FindReadOnlyByIdAsync(term1, cancellationToken).ConfigureAwait(false);
            var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
            var armsInSession = allArms.Where(arm => arm.SessionId == term!.SessionId).ToArray();

            var subjectIdsToCount = page.Select(subject => subject.Id).ToHashSet();
            var runningPupilsTaking = new Dictionary<Guid, int>();

            foreach (var arm in armsInSession)
            {
                var resolution = await resolver.ResolveAsync(arm.Id, term1, cancellationToken).ConfigureAwait(false);

                if (resolution.IsFailure)
                {
                    continue;
                }

                var resolvedSubjectIds = resolution.Value
                    .Select(row => row.SubjectId)
                    .Where(subjectIdsToCount.Contains)
                    .ToArray();

                if (resolvedSubjectIds.Length == 0)
                {
                    continue;
                }

                var armPupilCount = await enrolments.CountOpenExcludingPendingByArmAsync(arm.Id, cancellationToken).ConfigureAwait(false);

                foreach (var subjectId in resolvedSubjectIds)
                {
                    runningPupilsTaking[subjectId] = runningPupilsTaking.GetValueOrDefault(subjectId) + armPupilCount;
                }
            }

            pupilsTakingCountBySubject = runningPupilsTaking;
        }

        var items = page
            .Select(subject => SubjectMapper.ToDto(
                subject,
                termId is null ? null : mappedLevelCountBySubject.GetValueOrDefault(subject.Id),
                termId is null ? null : armExceptionCountBySubject.GetValueOrDefault(subject.Id),
                termId is null ? null : pupilsTakingCountBySubject.GetValueOrDefault(subject.Id)))
            .ToArray();

        var nextCursor = hasNextPage
            ? SubjectListCursor.Encode(page[^1].NameKey, page[^1].Id)
            : null;

        return Result.Success(new CursorPage<SubjectDto>(items, nextCursor));
    }

    private static bool IsAfterCursor(string nameKey, Guid id, string cursorNameKey, Guid cursorId)
    {
        var comparison = string.CompareOrdinal(nameKey, cursorNameKey);
        return comparison != 0 ? comparison > 0 : id.CompareTo(cursorId) > 0;
    }
}
