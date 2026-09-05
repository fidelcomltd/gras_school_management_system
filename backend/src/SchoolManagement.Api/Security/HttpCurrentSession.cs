using System.Security.Claims;
using SchoolManagement.Application.Abstractions.Identity;

namespace SchoolManagement.Api.Security;

/// <summary><see cref="ICurrentSession"/> over <c>HttpContext.User</c>'s <see cref="SessionClaimTypes.SessionId"/> claim.</summary>
internal sealed class HttpCurrentSession(IHttpContextAccessor httpContextAccessor) : ICurrentSession
{
    /// <inheritdoc />
    public Guid? SessionId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var claim = principal.FindFirstValue(SessionClaimTypes.SessionId);

            return Guid.TryParse(claim, out var sessionId) ? sessionId : null;
        }
    }
}
