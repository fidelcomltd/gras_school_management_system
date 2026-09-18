using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="GetArmQuery"/>.</summary>
internal sealed class GetArmHandler(IArmRepository arms, IClassLevelRepository levels)
    : IRequestHandler<GetArmQuery, Result<ArmDto>>
{
    /// <inheritdoc />
    public async Task<Result<ArmDto>> HandleAsync(GetArmQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arm = await arms.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (arm is null)
        {
            return Result.Failure<ArmDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelNamesById = allLevels.ToDictionary(level => level.Id, level => level.Name);

        return Result.Success(ArmMapper.ToDto(arm, levelNamesById));
    }
}
