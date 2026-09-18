namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>One arm's mark count for spec 6.6.6's "ending a mapping after marks have been entered" message.</summary>
/// <param name="ArmId">The arm carrying marks.</param>
/// <param name="ArmDisplayName">The arm's composed display name, for the rejection message.</param>
/// <param name="PupilCount">How many pupils in that arm have a recorded mark for the subject.</param>
public sealed record SubjectMarkArmSummary(Guid ArmId, string ArmDisplayName, int PupilCount);

/// <summary>
/// Which arms under a level have <c>subject_score</c> rows for a subject in a term — spec 6.6.6:
/// "ending a mapping for the active term when subject_score rows exist for that subject in any arm
/// under the level is rejected. The message names the arms and the counts."
/// </summary>
/// <remarks>
/// TASK-0076 dispatch A replaced the Infrastructure implementation with a real query against
/// <c>subject_score</c>/<c>result_set</c>, now that both tables exist — see
/// <c>SchoolManagement.Infrastructure.Results.SubjectMappingMarkLookup</c>. Before this card the
/// stand-in honestly answered empty unconditionally: no <c>subject_score</c> table existed anywhere
/// in this codebase, so that was today's only correct answer, not a placeholder one.
/// </remarks>
public interface ISubjectMappingMarkLookup
{
    /// <summary>
    /// Returns one entry per arm under <paramref name="classLevelId"/> that has at least one mark for
    /// <paramref name="subjectId"/> in <paramref name="termId"/>. Empty when none exist.
    /// </summary>
    Task<IReadOnlyList<SubjectMarkArmSummary>> FindArmsWithMarksAsync(
        Guid subjectId, Guid classLevelId, Guid termId, CancellationToken cancellationToken);
}
