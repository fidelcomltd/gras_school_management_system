using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>One subject resolved into effect for an arm in a term (spec 6.6.1, 6.6.4, 8.1).</summary>
public sealed record ResolvedArmSubject(
    Guid SubjectId, string SubjectName, string? SubjectCode, int DisplayOrder, SubjectSourceKind Source);

/// <summary>
/// THE single place spec 8.1's rule is expressed: "the subjects in effect for the arm this term" is
/// "level mappings for the term, plus the arm's include exceptions, minus its exclude exceptions."
/// No other caller may reimplement this — <c>GetArmSubjectsHandler</c>, the list view's
/// <c>pupilsTakingCount</c> and the eventual result-computation engine (TASK-0071) all call this.
/// </summary>
/// <remarks>
/// By the time this resolver runs, an include exception can never name a subject already covered by
/// an active level mapping, and an exclude exception can never name one that is not — both are
/// rejected at creation (spec 6.6.4's redundancy rule, enforced by <c>CreateSubjectExceptionHandler</c>)
/// — so a plain set union/difference is sufficient; no precedence rule between mapping and exception
/// is needed. Two arms of the same level may legitimately resolve to different sets (spec 8.4.7).
/// </remarks>
internal sealed class SubjectsInEffectResolver(
    IArmRepository arms,
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ISubjectMappingExceptionRepository exceptions)
{
    /// <summary>Resolves the subjects in effect for <paramref name="armId"/> in <paramref name="termId"/>.</summary>
    public async Task<Result<IReadOnlyList<ResolvedArmSubject>>> ResolveAsync(
        Guid armId, Guid termId, CancellationToken cancellationToken)
    {
        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);

        if (arm is null)
        {
            return Result.Failure<IReadOnlyList<ResolvedArmSubject>>(
                Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var levelMappings = await mappings
            .ListActiveByLevelAndTermReadOnlyAsync(arm.ClassLevelId, termId, cancellationToken)
            .ConfigureAwait(false);

        var armExceptions = await exceptions
            .ListByArmAndTermReadOnlyAsync(armId, termId, cancellationToken)
            .ConfigureAwait(false);

        var allSubjects = await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var subjectsById = allSubjects.ToDictionary(subject => subject.Id);

        var excludedIds = armExceptions
            .Where(exception => exception.Mode == SubjectExceptionMode.Exclude)
            .Select(exception => exception.SubjectId)
            .ToHashSet();

        var resolved = new List<ResolvedArmSubject>();

        foreach (var mapping in levelMappings.OrderBy(mapping => mapping.DisplayOrder))
        {
            if (excludedIds.Contains(mapping.SubjectId) || !subjectsById.TryGetValue(mapping.SubjectId, out var subject))
            {
                continue;
            }

            resolved.Add(new ResolvedArmSubject(subject.Id, subject.Name, subject.Code, mapping.DisplayOrder, SubjectSourceKind.LevelInherited));
        }

        // Arm-exception includes are appended after every level-inherited row, ordered by subject name
        // for a deterministic result — spec 6.6.4/6.6.9 give no row order for these (an exception
        // carries no display_order of its own), so this is this card's own judgement call, recorded
        // rather than left implicit.
        var nextDisplayOrder = (resolved.Count == 0 ? 0 : resolved.Max(row => row.DisplayOrder)) + 1;

        var includes = armExceptions
            .Where(exception => exception.Mode == SubjectExceptionMode.Include)
            .Select(exception => subjectsById.TryGetValue(exception.SubjectId, out var subject) ? subject : null)
            .Where(subject => subject is not null)
            .Select(subject => subject!)
            .OrderBy(subject => subject.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var subject in includes)
        {
            resolved.Add(new ResolvedArmSubject(subject.Id, subject.Name, subject.Code, nextDisplayOrder, SubjectSourceKind.ArmException));
            nextDisplayOrder++;
        }

        return Result.Success<IReadOnlyList<ResolvedArmSubject>>(resolved);
    }
}
