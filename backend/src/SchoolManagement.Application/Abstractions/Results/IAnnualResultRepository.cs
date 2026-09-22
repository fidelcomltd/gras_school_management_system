using SchoolManagement.Application.Results.Annual;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>One term of the arm's session, with the arm's own result set state for it (null when never scored).</summary>
public sealed record AnnualTermState(Guid TermId, int Ordinal, string TermName, ResultSetState? ArmResultSetState);

/// <summary>What the handler needs about the arm.</summary>
public sealed record AnnualArmContext(Guid ArmId, Guid SessionId, string LevelName, string ArmLabel, IReadOnlyList<AnnualTermState> Terms, string? ThirdTermSnapshotJson);

/// <summary>Loads and stores annual results.</summary>
public interface IAnnualResultRepository
{
    /// <summary>The arm, its session's terms and the arm's result set states; null when the arm does not exist.</summary>
    Task<AnnualArmContext?> LoadArmAsync(Guid armId, CancellationToken cancellationToken);

    /// <summary>
    /// The pupils with a result in the arm's Third Term set, each with every published term result they have anywhere
    /// in the session (term results travel with the pupil across arm moves).
    /// </summary>
    Task<IReadOnlyList<AnnualPupilInput>> LoadPupilsAsync(Guid armId, Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Deletes the session's rows for <paramref name="pupilIds"/> and stages <paramref name="rows"/>. Does NOT commit.</summary>
    Task ReplaceAsync(Guid sessionId, IReadOnlyCollection<Guid> pupilIds, IReadOnlyList<AnnualResult> rows, CancellationToken cancellationToken);
}
