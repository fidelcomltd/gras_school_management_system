using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="NextArmLabelQuery"/>.</summary>
internal sealed class NextArmLabelHandler(IArmRepository arms, IClassLevelRepository levels)
    : IRequestHandler<NextArmLabelQuery, Result<NextArmLabelResponse>>
{
    /// <inheritdoc />
    public async Task<Result<NextArmLabelResponse>> HandleAsync(NextArmLabelQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var levelId = Guid.Parse(request.LevelId);
        var sessionId = Guid.Parse(request.SessionId);

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        if (allLevels.All(level => level.Id != levelId))
        {
            return Result.Failure<NextArmLabelResponse>(Error.NotFound(
                "level.not_found", "No class level was found with that id."));
        }

        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);

        var existingLabels = allArms
            .Where(arm => arm.ClassLevelId == levelId && arm.SessionId == sessionId)
            .Select(arm => arm.Label);

        return Result.Success(new NextArmLabelResponse(ArmLabelSequencer.NextUnusedLabel(existingLabels)));
    }
}
