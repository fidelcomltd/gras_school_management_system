using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Adapts the static <see cref="PersistenceErrors"/> translation table to the
/// <see cref="IPersistenceErrorTranslator"/> port, so callers depend on an Application abstraction
/// rather than this Infrastructure type directly.
/// </summary>
/// <remarks>
/// The translation logic itself stays in <see cref="PersistenceErrors"/> as a plain static method —
/// it needs no state and no dependencies, so it is trivially unit-testable without DI. This class
/// exists only to give it an injectable seam for the Api layer's exception handler.
/// </remarks>
internal sealed class PersistenceErrorTranslator : IPersistenceErrorTranslator
{
    /// <inheritdoc />
    public Error? TryTranslate(Exception exception) => PersistenceErrors.TryTranslate(exception);
}
