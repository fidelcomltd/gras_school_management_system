using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Authorization;

/// <summary>
/// Source-generated log messages for the authorization seams in this folder. Same rules as the
/// Application layer's <c>BehaviorLog</c>: CA1848 is an error, so all logging goes through
/// <c>[LoggerMessage]</c>, and no parameter carries personal data — only ids, codes and paths.
/// </summary>
internal static partial class AuthorizationLog
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Warning,
        Message = "Privilege check rejected: user {UserId} lacks {Privilege} for {RoutePath}")]
    public static partial void PrivilegeCheckRejected(
        ILogger logger,
        string userId,
        string privilege,
        string routePath);
}
