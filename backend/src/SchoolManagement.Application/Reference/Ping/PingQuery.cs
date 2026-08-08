using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Reference.Ping;

/// <summary>
/// REFERENCE SLICE — the minimal query. Copy this shape for a read that needs no database.
/// </summary>
/// <remarks>
/// The whole slice lives in four files in this folder: the request (here, with its response DTO
/// and validator alongside), the handler, and its tests. Keep a slice's files together — the unit
/// of work is the feature, not the layer.
/// </remarks>
/// <param name="Name">Who to greet. Echoed back in the response message.</param>
public sealed record PingQuery(string Name) : IQuery<Result<PingResponse>>;

/// <summary>
/// Response to a <see cref="PingQuery"/>. Confirms the service is reachable and that its clock,
/// serialisation, and API version are what the caller expects.
/// </summary>
/// <param name="Message">
/// A greeting echoing the supplied name.
/// </param>
/// <param name="ServerTimeUtc">
/// Server time when the request was handled. Always UTC with an explicit offset (ISO-8601), per the
/// repo-wide rule that timestamps cross the wire as <see cref="DateTimeOffset"/> and are converted
/// to a local zone only in the UI.
/// </param>
/// <param name="ApiVersion">
/// The API version that served the request, matching the URL segment — useful when a client is
/// unsure which version a proxy routed it to.
/// </param>
public sealed record PingResponse(string Message, DateTimeOffset ServerTimeUtc, string ApiVersion);

/// <summary>
/// Validates <see cref="PingQuery"/>.
/// </summary>
/// <remarks>
/// EVERY request needs a validator, even a trivial one.
/// <c>ValidatorCoverageTests.EveryRequestHasAValidator</c> fails the build otherwise, so the
/// question "did anyone check this input?" always has the same answer.
/// </remarks>
internal sealed class PingQueryValidator : AbstractValidator<PingQuery>
{
    /// <summary>Longest accepted name. Mirrored in the OpenAPI schema.</summary>
    internal const int NameMaxLength = 100;

    /// <summary>Configures the rules.</summary>
    public PingQueryValidator()
    {
        RuleFor(query => query.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(NameMaxLength)
            .WithMessage($"Name must be at most {NameMaxLength} characters.");
    }
}
