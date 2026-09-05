using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SchoolManagement.Api.Security;

namespace SchoolManagement.UnitTests.Api.Security;

/// <summary>
/// THE BOOT-TIME CSRF GUARD TEST, modelled on <c>PrivilegeDeclarationGuardTests</c>. Proves
/// <see cref="CsrfDeclarationGuard"/> rejects a mutating route with neither
/// <c>RequireCsrfToken()</c> nor an explicit exemption — the failure mode
/// <c>POST /reference/records</c> demonstrated live in the committed contract.
/// </summary>
public sealed class CsrfDeclarationGuardTests
{
    [Fact]
    public void Validate_PassesWhenEveryMutatingRouteHasCsrfOrAnExplicitExemption()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapPost("/protected", () => Results.Ok()).RequireCsrfToken();
        app.MapPost("/scaffold", () => Results.Ok())
            .ExemptFromCsrfRequirement("Reference scaffold, deleted with the SampleRecord slice.");
        app.MapGet("/reads", () => Results.Ok());

        Should.NotThrow(() => CsrfDeclarationGuard.Validate(app));
    }

    [Fact]
    public void Validate_ThrowsAndNamesTheRoute_WhenAMutatingRouteHasNeither()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapPost("/protected", () => Results.Ok()).RequireCsrfToken();
        app.MapPost("/forgotten", () => Results.Ok());

        var exception = Should.Throw<InvalidOperationException>(() => CsrfDeclarationGuard.Validate(app));

        exception.Message.ShouldContain("/forgotten");
        exception.Message.ShouldNotContain("/protected");
    }

    [Fact]
    public void ExemptFromCsrfRequirement_RequiresANonEmptyReason()
    {
        var app = WebApplication.CreateBuilder().Build();
        var builder = app.MapPost("/scaffold", () => Results.Ok());

        Should.Throw<ArgumentException>(() => builder.ExemptFromCsrfRequirement(" "));
    }
}
