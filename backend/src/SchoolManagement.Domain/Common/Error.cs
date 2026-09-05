using System.Diagnostics.CodeAnalysis;

namespace SchoolManagement.Domain.Common;

/// <summary>
/// A single expected failure, carrying a stable machine-readable <paramref name="Code"/>.
/// </summary>
/// <param name="Code">
/// Stable, dot-separated, lower-case identifier — for example <c>sample_record.not_found</c>.
/// This is API surface: clients branch on it, so treat a rename as a breaking change.
/// Never put user-facing prose or PII here.
/// </param>
/// <param name="Description">Human-readable explanation. Safe to show a developer; never include PII.</param>
/// <param name="Type">Category that determines the HTTP status code. See <see cref="ErrorType"/>.</param>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Error' is the established name for this concept in the Result pattern, and " +
                    "renaming it to something like ResultError would make every handler in the " +
                    "codebase read worse. CA1716 protects cross-language consumers (Error is a VB " +
                    "statement keyword); this is a deployed service assembly with no public " +
                    "package surface and no VB consumers, so that risk does not apply. The rule " +
                    "stays enabled everywhere else.")]
public record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>Sentinel used by a successful <see cref="Result"/>. Never returned to a client.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Creates an unexpected failure (HTTP 500).</summary>
    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    /// <summary>Creates a validation failure (HTTP 422).</summary>
    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    /// <summary>Creates a not-found failure (HTTP 404).</summary>
    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    /// <summary>Creates a conflict failure (HTTP 409).</summary>
    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    /// <summary>Creates an authentication failure (HTTP 401).</summary>
    public static Error Unauthenticated(string code, string description) =>
        new(code, description, ErrorType.Unauthenticated);

    /// <summary>Creates an authorisation failure (HTTP 403).</summary>
    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    /// <summary>Creates a locked-account failure (HTTP 423). Prefer <see cref="LockedError"/> directly
    /// when the response needs to carry <c>lockedUntil</c>.</summary>
    public static Error Locked(string code, string description) =>
        new(code, description, ErrorType.Locked);
}

/// <summary>
/// A locked-account failure (spec 6.1.11) carrying the timestamp the lock expires at, so the
/// response can attach it as a <c>lockedUntil</c> extension member.
/// </summary>
/// <param name="LockedUntilUtc">When the lockout ends.</param>
public sealed record LockedError(DateTimeOffset LockedUntilUtc)
    : Error(LockedErrorCode, "This account is temporarily locked after too many failed attempts.", ErrorType.Locked)
{
    /// <summary>The stable error code for a lockout response.</summary>
    public const string LockedErrorCode = "auth.account_locked";
}

/// <summary>
/// A validation failure carrying per-field messages, produced by the validation behaviour
/// after aggregating every validator for a request.
/// </summary>
/// <param name="Failures">
/// Property name to the messages for that property. Keys are the request's property paths
/// as FluentValidation reports them (for example <c>PageSize</c>).
/// </param>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Failures)
    : Error(ValidationErrorCode, "One or more validation errors occurred.", ErrorType.Validation)
{
    /// <summary>The stable error code returned for every aggregated validation failure.</summary>
    public const string ValidationErrorCode = "request.validation_failed";
}
