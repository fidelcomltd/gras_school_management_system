namespace SchoolManagement.Application.Abstractions.Settings;

/// <summary>
/// Whether a rating scale is referenced by any rating block — spec 6.2.13's "each rating block
/// references a scale by id" implies a scale still in use must not simply vanish from underneath it.
/// Not spec-mandated wording; TASK-0072 stage 0's own addition, human-approved (open question 3).
/// </summary>
/// <remarks>
/// TASK-0072 STAGE 1's stand-in honestly answers "not in use" unconditionally
/// (<c>SchoolManagement.Infrastructure.Settings.RatingScaleUsageGate</c>) — no development domain or
/// trait block exists yet to reference anything, the same reasoning
/// <c>SchoolManagement.Application.Abstractions.Results.IPublishedResultsGate</c> documented before
/// <c>result_set</c> existed. Stage 2 (development domains) and stage 3 (traits) each extend the real
/// implementation to query their own new reference column once it exists, rather than this port
/// growing a new method per referencing table.
/// </remarks>
public interface IRatingScaleUsageGate
{
    /// <summary>
    /// Whether any rating block currently references the scale identified by
    /// <paramref name="ratingScaleId"/>.
    /// </summary>
    /// <param name="ratingScaleId">The scale to check.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<bool> IsInUseAsync(Guid ratingScaleId, CancellationToken cancellationToken);
}
