namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// A group of related endpoints. One module per feature.
/// </summary>
/// <remarks>
/// <para>
/// This is how <c>Program.cs</c> stays short. Modules are DISCOVERED by assembly scanning, so adding a
/// feature's endpoints means adding one file — there is no registration list to edit, and therefore no
/// merge conflict in a shared file every time two people add a feature.
/// </para>
/// <para>
/// RULES FOR A MODULE:
/// </para>
/// <list type="bullet">
/// <item>It must have a public parameterless constructor — it is instantiated by the container as a
/// singleton at startup, before any request exists.</item>
/// <item>It maps routes and nothing else. No business logic, no data access; the endpoint body sends a
/// request through <c>ISender</c> and matches the result.</item>
/// <item>It must declare full OpenAPI metadata for every endpoint (summary, description, operation ID,
/// tags, every response type). The committed contract is generated from this metadata, so an
/// undocumented endpoint produces an undocumented client.</item>
/// </list>
/// </remarks>
public interface IEndpointModule
{
    /// <summary>
    /// Maps this module's endpoints.
    /// </summary>
    /// <param name="endpoints">
    /// The route builder to map onto. Modules are given the VERSIONED group (for example
    /// <c>/api/v1</c>), so a module's own route templates are relative and contain no version segment.
    /// </param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
