using SchoolManagement.Application.Abstractions.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// TASK-0072 STAGE 1 stand-in for <see cref="IRatingScaleUsageGate"/> — honestly answers "not in use"
/// unconditionally. No development domain or trait block exists yet to reference a rating scale;
/// stage 2 (development domains) and stage 3 (traits) each extend this to query their own new
/// reference column once it exists, the same documented-seam shape
/// <c>SchoolManagement.Application.Abstractions.Results.IPublishedResultsGate</c> used before
/// <c>result_set</c> existed.
/// </summary>
internal sealed class RatingScaleUsageGate : IRatingScaleUsageGate
{
    /// <inheritdoc />
    public Task<bool> IsInUseAsync(Guid ratingScaleId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
