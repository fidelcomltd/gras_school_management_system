using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Api.Security;

/// <summary>
/// THE BOOT-TIME GUARD TEST. Spec 9.2: "A route with no declared privilege fails to register at
/// boot." Builds a bare, real <see cref="WebApplication"/> — no database, no configuration, no
/// hosted services — and maps routes directly onto it, which is enough to exercise
/// <see cref="PrivilegeDeclarationGuard"/> without starting the actual application.
/// </summary>
public sealed class PrivilegeDeclarationGuardTests
{
    [Fact]
    public void Validate_PassesWhenEveryRouteDeclaresAPrivilegeOrIsExplicitlyAnonymous()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGet("/protected", () => Results.Ok())
            .RequirePrivilege(Privileges.Arm.View, ScopeParameterKind.Arm, "armId");

        app.MapGet("/public", () => Results.Ok())
            .AllowAnonymous();

        Should.NotThrow(() => PrivilegeDeclarationGuard.Validate(app));
    }

    [Fact]
    public void Validate_ThrowsAndNamesTheRoute_WhenARouteDeclaresNeitherAPrivilegeNorAnonymous()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGet("/protected", () => Results.Ok())
            .RequirePrivilege(Privileges.Arm.View, ScopeParameterKind.Arm, "armId");

        // The defect under test: forgotten entirely. No AllowAnonymous, no RequirePrivilege — exactly
        // the pattern that, before TASK-0002, was silently "protected" only by the runtime fallback
        // policy.
        app.MapGet("/forgotten", () => Results.Ok());

        var exception = Should.Throw<InvalidOperationException>(() => PrivilegeDeclarationGuard.Validate(app));

        exception.Message.ShouldContain("/forgotten");
    }

    [Fact]
    public void Validate_ReportsEveryOffendingRoute_NotJustTheFirst()
    {
        var app = WebApplication.CreateBuilder().Build();

        app.MapGet("/forgotten-one", () => Results.Ok());
        app.MapGet("/forgotten-two", () => Results.Ok());

        var exception = Should.Throw<InvalidOperationException>(() => PrivilegeDeclarationGuard.Validate(app));

        exception.Message.ShouldContain("/forgotten-one");
        exception.Message.ShouldContain("/forgotten-two");
    }
}
