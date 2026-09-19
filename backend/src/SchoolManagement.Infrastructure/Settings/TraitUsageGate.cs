using SchoolManagement.Application.Abstractions.Settings;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// TASK-0072 STAGE 3B stand-in for <see cref="ITraitUsageGate"/> — honestly answers "never rated"
/// unconditionally. No trait-rating table exists yet; Phase 3's entry screens replace this with a real
/// query once it does, the same documented-seam shape <see cref="DevelopmentIndicatorUsageGate"/> used
/// before its own rating table existed.
/// </summary>
internal sealed class TraitUsageGate : ITraitUsageGate
{
    /// <inheritdoc />
    public Task<bool> HasEverBeenRatedAsync(Guid traitId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
