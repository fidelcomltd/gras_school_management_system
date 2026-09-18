using SchoolManagement.Application.Abstractions.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// TASK-0072 STAGE 2A stand-in for <see cref="IDevelopmentIndicatorUsageGate"/> — honestly answers
/// "never rated" unconditionally. No <c>development_indicator_rating</c> table exists yet; Phase 3's
/// entry screens replace this with a real query once it does, the same documented-seam shape
/// <see cref="RatingScaleUsageGate"/> used before <c>development_domain</c> existed.
/// </summary>
internal sealed class DevelopmentIndicatorUsageGate : IDevelopmentIndicatorUsageGate
{
    /// <inheritdoc />
    public Task<bool> HasEverBeenRatedAsync(Guid indicatorId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
