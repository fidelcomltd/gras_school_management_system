using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Authorization;
using SchoolManagement.Application.Behaviors;
using SchoolManagement.Application.Idempotency;
using SchoolManagement.Application.Messaging;

namespace SchoolManagement.Application;

/// <summary>
/// Registers the application layer: the mediator, its behaviours, handlers, and validators.
/// </summary>
public static class ApplicationDependencyInjection
{
    /// <summary>
    /// The Application assembly. Used for handler/validator scanning here and by the architecture
    /// tests, so both look at exactly the same assembly.
    /// </summary>
    public static Assembly Assembly => typeof(ApplicationDependencyInjection).Assembly;

    /// <summary>
    /// Adds the mediator, the pipeline behaviours, and every handler and validator in this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ISender, Sender>();

        // TASK-0002: server-side scope resolution (spec 4.2.1), consumed by the Api project's
        // privilege authorization handler.
        services.AddScoped<IScopeResolver, ScopeResolver>();

        AddPipelineBehaviors(services);

        services.AddValidatorsFromAssembly(Assembly, includeInternalTypes: true);
        AddRequestHandlers(services);

        services.AddSingleton<IValidateOptions<PipelineOptions>, PipelineOptionsValidator>();

        // TASK-0019: bound and validated in Program.cs (the composition root), the same way
        // PipelineOptions is above — only the validator's registration lives here.
        services.AddSingleton<IValidateOptions<IdempotencyOptions>, IdempotencyOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Registers the behaviour pipeline. <b>ORDER IS THE CONTRACT — READ BEFORE EDITING.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The FIRST registered behaviour is the OUTERMOST: it sees the request first and the response
    /// last. Adding a behaviour in the wrong position changes semantics silently, so
    /// <c>PipelineOrderTests</c> asserts this exact sequence and will fail if you reorder it
    /// without updating the test deliberately.
    /// </para>
    /// <list type="number">
    /// <item><b>UnhandledException</b> — outermost, so it observes failures in every stage below,
    /// including a behaviour that throws rather than only the handler.</item>
    /// <item><b>RequestLogging</b> — records the attempt and its outcome. Inside exception logging
    /// so an exception is reported once, by the stage whose job that is.</item>
    /// <item><b>Validation</b> — rejects bad input before anything expensive happens. Nothing below
    /// this line ever sees an invalid request, which is why handlers contain no input checks.</item>
    /// <item><b>Performance</b> — times the handler and its database work, excluding validation,
    /// which is in-memory and constant-time.</item>
    /// <item><b>UnitOfWork</b> — innermost, and COMMANDS ONLY (constrained to
    /// <see cref="IBaseCommand"/>), so a database transaction is held for the shortest possible
    /// window and a query can never acquire one.</item>
    /// </list>
    /// <para>
    /// Registered as open generics, so one registration covers every request type — a new request
    /// automatically gets the full pipeline with no wiring step to forget.
    /// </para>
    /// </remarks>
    private static void AddPipelineBehaviors(IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
    }

    /// <summary>
    /// Scans for <see cref="IRequestHandler{TRequest, TResponse}"/> implementations and registers
    /// each against the closed interface it implements.
    /// </summary>
    /// <remarks>
    /// Assembly scanning rather than explicit registration: a hand-maintained list is the kind of
    /// file that silently rots, and forgetting a line produces a runtime "no handler registered"
    /// error instead of a compile error. Scanning means adding a handler is a one-file change.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Two handlers claim the same request type. Ambiguity here would otherwise resolve to
    /// "whichever the container happened to register last", so it is a hard failure at startup.
    /// </exception>
    private static void AddRequestHandlers(IServiceCollection services)
    {
        var handlerInterface = typeof(IRequestHandler<,>);

        var registrations = Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(@interface => @interface.IsGenericType &&
                                     @interface.GetGenericTypeDefinition() == handlerInterface)
                .Select(@interface => (Service: @interface, Implementation: type)))
            .ToArray();

        var duplicates = registrations
            .GroupBy(registration => registration.Service)
            .Where(group => group.Count() > 1)
            .ToArray();

        if (duplicates.Length > 0)
        {
            var detail = string.Join(
                "; ",
                duplicates.Select(group =>
                    $"{group.Key} is handled by " +
                    string.Join(" and ", group.Select(registration => registration.Implementation.Name))));

            throw new InvalidOperationException(
                $"Each request must have exactly one handler, but found duplicates: {detail}.");
        }

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }
    }
}
