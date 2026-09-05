using SchoolManagement.Application.Abstractions.Identity;

namespace SchoolManagement.Api.Bootstrap;

/// <summary>
/// <see cref="ICurrentUser"/> for a process with no HTTP caller at all — spec 6.1.3: "created_by...
/// Null for the bootstrap account only." Registered only in <see cref="BootstrapAdminAccountCli"/>'s
/// composition (built in Program.cs), never in the web host's own DI container.
/// </summary>
internal sealed class NoOneCurrentUser : ICurrentUser
{
    public string? UserId => null;
    public bool IsAuthenticated => false;
}
