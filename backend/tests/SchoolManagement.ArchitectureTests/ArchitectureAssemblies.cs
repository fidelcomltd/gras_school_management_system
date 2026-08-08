using System.Reflection;

namespace SchoolManagement.ArchitectureTests;

/// <summary>
/// The assemblies under test, resolved once.
/// </summary>
/// <remarks>
/// Resolved from a TYPE in each assembly rather than by name string. A typo in
/// <c>Assembly.Load("SchoolManagment.Domain")</c> throws at runtime; a typo in a type name does not
/// compile. More importantly, a renamed project keeps working here instead of silently making every
/// architecture rule vacuous — a test suite that passes because it examined nothing is worse than no
/// test suite, because it reports success.
/// </remarks>
internal static class ArchitectureAssemblies
{
    public static Assembly Domain => typeof(Domain.Common.Result).Assembly;

    public static Assembly Application => typeof(Application.ApplicationDependencyInjection).Assembly;

    public static Assembly Infrastructure =>
        typeof(Infrastructure.InfrastructureDependencyInjection).Assembly;

    public static Assembly Api => typeof(Api.Endpoints.IEndpointModule).Assembly;

    public static string DomainNamespace => "SchoolManagement.Domain";

    public static string ApplicationNamespace => "SchoolManagement.Application";

    public static string InfrastructureNamespace => "SchoolManagement.Infrastructure";

    public static string ApiNamespace => "SchoolManagement.Api";
}
