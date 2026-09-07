using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.Roles;

/// <summary>Handles <see cref="GetRoleQuery"/>.</summary>
internal sealed class GetRoleQueryHandler(IRoleRepository roles) : IRequestHandler<GetRoleQuery, Result<RoleDto>>
{
    /// <inheritdoc />
    public async Task<Result<RoleDto>> HandleAsync(GetRoleQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = await roles.FindReadOnlyByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (role is null)
        {
            return Result.Failure<RoleDto>(Error.NotFound("role.not_found", "No role was found with that id."));
        }

        return Result.Success(RoleMapper.ToDto(role));
    }
}
