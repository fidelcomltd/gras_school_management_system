using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// THE test that makes "every request is validated" true rather than aspirational.
/// </summary>
/// <remarks>
/// The validation behaviour lets a request with no validator pass through, because failing at runtime
/// would turn a forgotten validator into a production 500. This test is the other half of that
/// decision: the mistake is caught here, at build time, where it costs nothing.
/// <para>
/// If you are reading this because the test just failed: add a validator for the named request. If the
/// request genuinely has nothing to validate, add one with no rules — that is a two-line file, and it
/// records that somebody CONSIDERED the question. Do not add an exemption list.
/// </para>
/// </remarks>
public sealed class ValidatorCoverageTests
{
    [Fact]
    public void EveryRequestHasAValidator()
    {
        var requestTypes = ArchitectureAssemblies.Application
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(type => type.GetInterfaces().Any(IsRequestInterface))
            .ToArray();

        // Sanity check on the test itself. If a refactor moved the requests to another assembly, the
        // query above would return nothing and this test would "pass" while checking nothing at all.
        requestTypes.ShouldNotBeEmpty(
            "No request types were found in the Application assembly. The test is looking in the wrong " +
            "place — fix the test rather than assuming there is nothing to check.");

        var validatedTypes = ArchitectureAssemblies.Application
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(@interface => @interface.IsGenericType &&
                                 @interface.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(@interface => @interface.GetGenericArguments()[0])
            .ToHashSet();

        var unvalidated = requestTypes
            .Where(type => !validatedTypes.Contains(type))
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        unvalidated.ShouldBeEmpty(
            "Every request must have a FluentValidation validator, even an empty one. " +
            $"Missing for:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unvalidated.Select(name => "  - " + name)));
    }

    [Fact]
    public void EveryRequestReturnsAResult()
    {
        // Belt and braces: IRequest<TResponse> already constrains TResponse to Result, so this cannot
        // currently fail. It is here so that if someone relaxes that constraint to "make something
        // work", the consequence is a failing test with an explanation rather than a handler that
        // throws for expected failures and an endpoint that cannot map the outcome.
        var offenders = ArchitectureAssemblies.Application
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces().Where(IsRequestInterface).Select(
                @interface => (Type: type, Response: @interface.GetGenericArguments()[0])))
            .Where(pair => !typeof(Domain.Common.Result).IsAssignableFrom(pair.Response))
            .Select(pair => $"{pair.Type.Name} returns {pair.Response.Name}")
            .ToArray();

        offenders.ShouldBeEmpty(
            "Every request must return Result or Result<T> so expected failures travel as values " +
            $"rather than exceptions. Offenders: {string.Join("; ", offenders)}");
    }

    private static bool IsRequestInterface(Type candidate) =>
        candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequest<>);
}
