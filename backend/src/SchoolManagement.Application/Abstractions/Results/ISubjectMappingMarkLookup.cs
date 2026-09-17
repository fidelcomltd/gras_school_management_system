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
/// THIS IS A DOCUMENTED SEAM, NOT A STAND-IN PRETENDING TO BE REAL — the same pattern
/// <c>ISubjectScoreSessionLockLookup</c> establishes (TASK-0069), not <c>IResultSetArmLookup</c>'s
/// throwing stand-in: no <c>subject_score</c> table exists anywhere in this codebase as of TASK-0070,
/// so "no marks exist for this subject in any arm" is today's only CORRECT answer, not a placeholder
/// one — the same reasoning <c>ISubjectScoreSessionLockLookup</c>'s own remarks give. Both branches of
/// the handler that calls this (marks found vs. not found) are unit-tested against a fake, because the
/// real implementation can never exercise the "found" branch until scoring exists.
/// <para>
/// Whichever future card first persists a <c>subject_score</c> row must replace the Infrastructure
/// implementation with a real query grouped by arm and pupil count, and add the integration test this
/// seam cannot carry today.
/// </para>
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
