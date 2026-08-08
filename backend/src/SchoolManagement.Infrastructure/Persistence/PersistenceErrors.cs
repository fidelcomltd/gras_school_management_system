using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Translates database exceptions into domain <see cref="Error"/> values.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS HERE: the global exception handler in the Api project needs to turn a unique-key
/// violation into a 409 rather than a 500. Doing that in the Api layer would require it to reference
/// <c>DbUpdateException</c> and <c>PostgresException</c> — spreading knowledge of the storage
/// technology into the HTTP surface. Instead the knowledge stays in Infrastructure and only a
/// <see cref="Domain"/> type crosses the boundary.
/// </para>
/// <para>
/// This is a SAFETY NET, not the primary mechanism. Handlers should check for a conflict and return a
/// clear <see cref="ErrorType.Conflict"/> result themselves. But a read-then-write check is racy, so
/// two concurrent requests can both pass it — and the database constraint is what actually holds the
/// line. This turns the resulting exception into the same response the handler would have produced.
/// </para>
/// </remarks>
public static class PersistenceErrors
{
    // PostgreSQL SQLSTATE codes. See https://www.postgresql.org/docs/current/errcodes-appendix.html
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string NotNullViolation = "23502";
    private const string CheckViolation = "23514";

    /// <summary>
    /// Returns a domain error for a recognised database failure, or <c>null</c> when the exception is
    /// not a database constraint problem and should be treated as an unexpected fault.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <remarks>
    /// The <c>Description</c> values here are deliberately generic. A PostgreSQL constraint-violation
    /// message names the constraint, the table and often the conflicting VALUE — which can be personal
    /// data, and which tells an attacker about the schema. The real detail is logged server-side; the
    /// client gets a stable code.
    /// </remarks>
    public static Error? TryTranslate(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            DbUpdateConcurrencyException => Error.Conflict(
                "persistence.concurrency_conflict",
                "The resource was modified by another request. Re-read it and retry."),

            DbUpdateException { InnerException: PostgresException postgres } =>
                TranslatePostgres(postgres),

            PostgresException postgres => TranslatePostgres(postgres),

            _ => null,
        };
    }

    private static Error? TranslatePostgres(PostgresException exception) => exception.SqlState switch
    {
        UniqueViolation => Error.Conflict(
            "persistence.duplicate_value",
            "A resource with the same unique value already exists."),

        ForeignKeyViolation => Error.Conflict(
            "persistence.related_resource_conflict",
            "The operation conflicts with a related resource."),

        // A null or check violation means our validation let through something the database refused.
        // That is our bug, not the caller's, so it is NOT mapped to a 4xx: returning null sends it
        // down the unexpected-failure path where it is logged loudly and answered with a 500.
        NotNullViolation or CheckViolation => null,

        _ => null,
    };
}
