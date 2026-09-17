using SchoolManagement.Application.Abstractions.Results;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// Default <see cref="ISubjectMappingMarkLookup"/>: honestly empty. See the interface's remarks — no
/// <c>subject_score</c> table exists anywhere in this codebase as of TASK-0070, so "no arm has a mark
/// for this subject" is today's only correct answer, not a stand-in. Replace this with a real query,
/// grouped by arm and pupil count, when the scoring module lands.
/// </summary>
internal sealed class SubjectMappingMarkLookup : ISubjectMappingMarkLookup
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SubjectMarkArmSummary>> FindArmsWithMarksAsync(
        Guid subjectId, Guid classLevelId, Guid termId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SubjectMarkArmSummary>>([]);
}
