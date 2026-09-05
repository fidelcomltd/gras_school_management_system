namespace SchoolManagement.Api.Endpoints;

/// <summary>
/// Discovery and registration for <see cref="IEndpointModule"/>.
/// </summary>
public static class EndpointModuleExtensions
{
    /// <summary>
    /// Finds every <see cref="IEndpointModule"/> in the Api assembly and registers it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// A module has no public parameterless constructor. Reported at startup with the offending type
    /// named, rather than as an obscure container error at first request.
    /// </exception>
    public static IServiceCollection AddEndpointModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var moduleTypes = typeof(EndpointModuleExtensions).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } &&
                typeof(IEndpointModule).IsAssignableFrom(type))
            .ToArray();

        foreach (var moduleType in moduleTypes)
        {
            if (moduleType.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"Endpoint module '{moduleType.Name}' must have a public parameterless " +
                    "constructor. Modules are constructed at startup to map routes and must not take " +
                    "dependencies — resolve per-request services in the endpoint delegate's parameters " +
                    "instead, where the container can scope them correctly.");
            }

            services.AddSingleton(typeof(IEndpointModule), moduleType);
        }

        return services;
    }

    /// <summary>
    /// Invokes every registered module against <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">The versioned route group modules should map onto.</param>
    /// <param name="services">The application's service provider.</param>
    /// <param name="logger">Logger used to record what was mapped.</param>
    public static void MapEndpointModules(
        this IEndpointRouteBuilder endpoints,
        IServiceProvider services,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(services);

        var modules = services.GetServices<IEndpointModule>().ToArray();

        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        // Logged at startup so "is my endpoint registered?" is answerable from the logs. A module that
        // silently fails to be discovered is otherwise invisible until a 404 appears. Guarded (CA1873):
        // the joined module-name string is a computed argument, hoisted to a local inside the guard.
        if (logger.IsEnabled(LogLevel.Information))
        {
            var moduleNames = string.Join(", ", modules.Select(module => module.GetType().Name));
            ApiLog.RegisteredEndpointModules(logger, modules.Length, moduleNames);
        }
    }
}
