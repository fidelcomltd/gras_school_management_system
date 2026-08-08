using NetArchTest.Rules;
using SchoolManagement.Application.Abstractions.Messaging;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Conventions for mediator handlers, validators and requests.
/// </summary>
public sealed class HandlerConventionTests
{
    [Fact]
    public void Handlers_AreSealed()
    {
        var result = Types.InAssembly(ArchitectureAssemblies.Application)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .BeSealed()
            .GetResult();

        result.ShouldBeSuccessful(
            "Handlers must be sealed. A handler is a leaf: inheriting from one to share code produces " +
            "a base class whose behaviour is invisible at the call site. Share code through an injected " +
            "service instead.");
    }

    [Fact]
    public void Handlers_AreNotPublic()
    {
        var result = Types.InAssembly(ArchitectureAssemblies.Application)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .NotBePublic()
            .GetResult();

        result.ShouldBeSuccessful(
            "Handlers must be internal. They are reached only through ISender; a public handler invites " +
            "an endpoint to call it directly, which skips validation, logging, timing and the " +
            "transaction — every guarantee the pipeline provides.");
    }

    [Fact]
    public void Validators_AreSealed()
    {
        var result = Types.InAssembly(ArchitectureAssemblies.Application)
            .That()
            .Inherit(typeof(FluentValidation.AbstractValidator<>))
            .Should()
            .BeSealed()
            .GetResult();

        result.ShouldBeSuccessful("Validators must be sealed, for the same reason handlers are.");
    }

    [Fact]
    public void Requests_AreRecords_SoTheyAreValueEqualAndImmutable()
    {
        // A request is a message: it should be immutable and compare by value. Records give both for
        // free, and the compiler-generated Equals is what makes caching or de-duplicating a request
        // possible later without revisiting every request type.
        var mutableRequests = ArchitectureAssemblies.Application
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetInterfaces().Any(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == typeof(IRequest<>)))
            // A record has a compiler-generated Clone method; nothing else does. This is the only
            // reliable reflection-time signal that a type was declared as a record.
            .Where(type => type.GetMethod("<Clone>$", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic) is null)
            .Select(type => type.FullName)
            .ToArray();

        mutableRequests.ShouldBeEmpty(
            "Every request must be declared as a record so it is immutable and compares by value. " +
            $"These are not: {string.Join(", ", mutableRequests)}");
    }
}
