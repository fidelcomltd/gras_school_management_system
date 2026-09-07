using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Security.PrivilegeRegister;

/// <summary>Handles <see cref="GetPrivilegeRegisterQuery"/>.</summary>
/// <remarks>
/// Reads <see cref="PrivilegeRegistry.All"/> only — no repository, no database. LINQ's
/// <c>GroupBy</c> preserves the source sequence's order both across groups and within each group
/// (documented behaviour, not an implementation accident), and <see cref="PrivilegeRegistry.All"/>
/// is already listed in spec 4.4.1-4.4.6 order, so no explicit sort is needed here.
/// </remarks>
internal sealed class GetPrivilegeRegisterQueryHandler
    : IRequestHandler<GetPrivilegeRegisterQuery, Result<PrivilegeRegisterResponse>>
{
    /// <inheritdoc />
    public Task<Result<PrivilegeRegisterResponse>> HandleAsync(
        GetPrivilegeRegisterQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var groups = PrivilegeRegistry.All
            .GroupBy(definition => definition.Module)
            .Select(group => new PrivilegeGroupDto(
                PrivilegeModuleCatalog.KeyFor(group.Key),
                PrivilegeModuleCatalog.TitleFor(group.Key),
                group
                    .Select(definition => new PrivilegeDescriptorDto(definition.Code, definition.Permits, definition.Scopable))
                    .ToArray()))
            .ToArray();

        return Task.FromResult(Result.Success(new PrivilegeRegisterResponse(groups)));
    }
}
