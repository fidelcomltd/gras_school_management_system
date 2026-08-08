using NetArchTest.Rules;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// Enforces the layering. These rules are the reason the layering survives contact with deadlines:
/// a reviewer may miss a stray <c>using</c>, the build does not.
/// </summary>
public sealed class DependencyDirectionTests
{
    [Fact]
    public void Domain_DependsOnNothing()
    {
        // The strictest rule in the codebase, and the one that keeps the domain testable and portable.
        // The moment Domain references EF Core or ASP.NET Core, business rules can only be exercised by
        // starting infrastructure.
        var result = Types.InAssembly(ArchitectureAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                ArchitectureAssemblies.ApplicationNamespace,
                ArchitectureAssemblies.InfrastructureNamespace,
                ArchitectureAssemblies.ApiNamespace,
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "Npgsql",
                "FluentValidation",
                "Serilog")
            .GetResult();

        result.ShouldBeSuccessful(
            "Domain must have no outward dependencies at all — not even Microsoft.Extensions. " +
            "If a domain type needs logging, configuration or persistence, the dependency is pointing " +
            "the wrong way: express what it needs as an abstraction in Application instead.");
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(ArchitectureAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                ArchitectureAssemblies.InfrastructureNamespace,
                ArchitectureAssemblies.ApiNamespace)
            .GetResult();

        result.ShouldBeSuccessful(
            "Application must not know about Infrastructure or Api. It declares abstractions " +
            "(IUnitOfWork, ISecretProvider, repository interfaces) and Infrastructure implements them. " +
            "A reference in this direction makes the use cases untestable without a database.");
    }

    [Fact]
    public void Application_DoesNotReferenceEntityFrameworkCore()
    {
        // Called out separately from the rule above because it is the one most likely to be broken with
        // good intentions — exposing DbSet<T> on an IApplicationDbContext interface is a popular
        // shortcut that drags query-provider semantics and EF's change tracker into the use-case layer.
        var result = Types.InAssembly(ArchitectureAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.ShouldBeSuccessful(
            "Application must not reference EF Core or Npgsql. Persistence belongs behind an " +
            "abstraction; see ISampleRecordRepository and IUnitOfWork for the intended shape.");
    }

    [Fact]
    public void NothingDependsOnApi()
    {
        // Api is the composition root: it is allowed to reference everything, and nothing may reference
        // it. If Infrastructure needed something from Api, that something is in the wrong project.
        var result = Types
            .InAssemblies([
                ArchitectureAssemblies.Domain,
                ArchitectureAssemblies.Application,
                ArchitectureAssemblies.Infrastructure,
            ])
            .ShouldNot()
            .HaveDependencyOn(ArchitectureAssemblies.ApiNamespace)
            .GetResult();

        result.ShouldBeSuccessful(
            "Nothing may depend on the Api project. It is the composition root and the HTTP surface; " +
            "a dependency on it means a lower layer has taken on an HTTP concern.");
    }

    [Fact]
    public void Domain_DoesNotDependOnApplicationAbstractions()
    {
        var result = Types.InAssembly(ArchitectureAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOn($"{ArchitectureAssemblies.ApplicationNamespace}.Abstractions")
            .GetResult();

        result.ShouldBeSuccessful("Domain must not reach into Application's abstractions.");
    }
}
