using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SchoolManagement.Api.Endpoints;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Api.Endpoints;

/// <summary>
/// Pins that approve and return are SCHOOL-WIDE routes (coordinator amendment, TASK-0090 review: an
/// arm-scoped grant would let a class teacher approve their own class, exactly the separation spec
/// 6.7.8 exists for). Builds a bare, real <see cref="WebApplication"/> — same technique
/// <c>PrivilegeDeclarationGuardTests</c> uses — and reads each route's declared
/// <see cref="PrivilegeRequirement"/> metadata directly, so a future edit that re-adds a scope
/// parameter to either route fails here rather than only being caught by a slower integration test.
/// </summary>
public sealed class ResultSetEndpointsScopeTests
{
    [Theory]
    [InlineData("/result-sets/{resultSetId:guid}/approve", Privileges.Results.Approve)]
    [InlineData("/result-sets/{resultSetId:guid}/return", Privileges.Results.Return)]
    [InlineData("/result-sets/{resultSetId:guid}/publish", Privileges.Results.Publish)]
    [InlineData("/result-sets/{resultSetId:guid}/withdraw", Privileges.Results.Unpublish)]
    [InlineData("/result-sets/{resultSetId:guid}/reopen", Privileges.Results.Unpublish)]
    public void ApproveAndReturn_DeclareNoScopeParameter(string routePattern, string expectedPrivilege)
    {
        var builder = WebApplication.CreateBuilder();
        // The route delegate takes ISender as a service parameter — minimal-API metadata inference
        // (triggered merely by enumerating .Endpoints below, not by handling a request) needs it
        // registered as a real service to recognise it as [FromServices] rather than failing to bind.
        builder.Services.AddSingleton(Substitute.For<ISender>());
        var app = builder.Build();
        new ResultSetEndpoints().MapEndpoints(app);

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == routePattern);

        var requirement = endpoint.Metadata.GetMetadata<PrivilegeRequirement>();

        requirement.ShouldNotBeNull();
        requirement!.Privilege.ShouldBe(expectedPrivilege);
        requirement.ScopeParameterKind.ShouldBe(ScopeParameterKind.None);
        requirement.RouteParameterName.ShouldBeNull();
    }
}
