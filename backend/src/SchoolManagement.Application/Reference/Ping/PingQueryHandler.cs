using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reference.Ping;

/// <summary>
/// Handles <see cref="PingQuery"/>.
/// </summary>
/// <remarks>
/// <para>
/// THINGS TO COPY FROM THIS HANDLER:
/// </para>
/// <list type="bullet">
/// <item><c>internal sealed</c> — a handler is reached only through <see cref="ISender"/>. Enforced
/// by <c>HandlerConventionTests</c>.</item>
/// <item>No input validation. The pipeline guarantees the request is already valid, so a guard here
/// would be dead code that implies the opposite.</item>
/// <item><see cref="TimeProvider"/> instead of <c>DateTimeOffset.UtcNow</c>. Ambient time is what
/// makes a test that passes at 23:59 fail at 00:01; <c>NoAmbientDateTimeTests</c> fails the build
/// on direct clock access, and tests inject <c>FakeTimeProvider</c>.</item>
/// <item>Returns <see cref="Result"/>. Even though this handler cannot fail, the signature keeps the
/// contract uniform for callers and for the endpoint's result mapping.</item>
/// <item>Accepts and forwards the <see cref="CancellationToken"/>, even when unused today.</item>
/// </list>
/// </remarks>
internal sealed class PingQueryHandler(TimeProvider timeProvider)
    : IRequestHandler<PingQuery, Result<PingResponse>>
{
    /// <summary>The API version this handler is served under. Kept in one place per slice.</summary>
    private const string ApiVersion = "1.0";

    /// <inheritdoc />
    public Task<Result<PingResponse>> HandleAsync(PingQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = new PingResponse(
            Message: $"Hello, {request.Name}.",
            ServerTimeUtc: timeProvider.GetUtcNow(),
            ApiVersion: ApiVersion);

        return Task.FromResult(Result.Success(response));
    }
}
