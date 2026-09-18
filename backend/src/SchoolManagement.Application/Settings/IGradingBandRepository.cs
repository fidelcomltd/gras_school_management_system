using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>Persistence port for the grading scale (spec 6.2.5). Implemented in Infrastructure.</summary>
/// <remarks>
/// <c>PUT /settings/grading</c> and <c>POST /settings/grading/reset</c> both replace the WHOLE set —
/// 6.2.12: "Whole scale as one array. Atomic." — so this port has no per-row update method; a band's
/// id has no meaning that survives a save (see <see cref="GradingBand"/>'s remarks), unlike
/// <see cref="IAssessmentComponentRepository"/> where identity must be preserved for the session lock.
/// </remarks>
public interface IGradingBandRepository
{
    /// <summary>Loads every band, read-only, ordered by <see cref="GradingBand.DisplayOrder"/>.</summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<IReadOnlyList<GradingBand>> ListReadOnlyOrderedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every existing row with <paramref name="bands"/>. Does NOT commit — the unit-of-work
    /// behaviour does that when the command returns a successful result, in the SAME transaction as
    /// the <see cref="ConfigVersion"/> row and <see cref="SchoolProfile.IncrementGradingVersion"/>.
    /// </summary>
    /// <param name="bands">The whole new scale, already validated by <see cref="GradingScaleRules"/>.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task ReplaceAllAsync(IReadOnlyList<GradingBand> bands, CancellationToken cancellationToken);
}
