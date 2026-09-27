using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Abstractions.Promotion;

/// <summary>Persistence for end-of-session promotion (spec 6.3.7) and the facts its reversal rule reads.</summary>
public interface IPromotionRepository
{
    /// <summary>The committed (not reversed) batch promoting from <paramref name="sourceSessionId"/>, with its decisions, read-only; at most one exists.</summary>
    Task<PromotionBatch?> FindCommittedForSessionAsync(Guid sourceSessionId, CancellationToken cancellationToken);

    /// <summary>A batch with its decisions, tracked, for reversal.</summary>
    Task<PromotionBatch?> FindTrackedWithDecisionsAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>Stages a new batch (and its decisions). Does NOT commit.</summary>
    Task AddAsync(PromotionBatch batch, CancellationToken cancellationToken);

    /// <summary>The session's annual results, one per pupil (spec 6.7.10), read-only.</summary>
    Task<IReadOnlyList<AnnualResult>> ListAnnualResultsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Whether any mark has been entered in any term of <paramref name="sessionId"/>: the first bar to reversal.</summary>
    Task<bool> AnyMarkInSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Whether a pin of <paramref name="sessionId"/> has been used against any of these pupils: the second bar to reversal.</summary>
    Task<bool> AnyPinUseInSessionAsync(Guid sessionId, IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an enrolment at once, inside the request's transaction (reversal removes the enrolments a batch opened). Immediate, so
    /// the reopened enrolment never meets it under the one-open-enrolment index.
    /// </summary>
    Task RemoveEnrolmentAsync(Guid enrolmentId, CancellationToken cancellationToken);

    /// <summary>The earliest session starting after <paramref name="startDate"/>: the default promotion target.</summary>
    Task<AcademicSession?> FindNextSessionAsync(DateOnly startDate, CancellationToken cancellationToken);

    /// <summary>Pupils who were enrolled in an arm of <paramref name="sessionId"/> but are no longer active (spec 6.3.9: listed apart).</summary>
    Task<IReadOnlyList<Pupil>> ListInactiveEnrolledInSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
