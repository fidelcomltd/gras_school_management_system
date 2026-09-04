using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Abstractions.Persistence;

/// <summary>
/// Translates a persistence-layer exception into a domain <see cref="Error"/>, or reports that it is
/// not a recognised database failure.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction exists so the Api layer's global exception handler never names an Infrastructure
/// type — the same reason <see cref="IUnitOfWork"/> keeps EF Core out of Application. The Api project's
/// <c>.csproj</c> header scopes its Infrastructure reference to composition-root wiring only; a handler
/// on the request pipeline calling into <c>SchoolManagement.Infrastructure.Persistence</c> directly is
/// outside that scope, which is exactly what happened before this port was introduced.
/// </para>
/// <para>
/// Implemented over EF Core / Npgsql exception types in Infrastructure. See
/// <c>DependencyDirectionTests.Api_DoesNotDependOnInfrastructureOutsideComposition</c> for the rule
/// this keeps enforced.
/// </para>
/// </remarks>
public interface IPersistenceErrorTranslator
{
    /// <summary>
    /// Returns a domain error for a recognised database constraint failure, or <c>null</c> when
    /// <paramref name="exception"/> is not one and should be treated as an unexpected fault.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    Error? TryTranslate(Exception exception);
}
