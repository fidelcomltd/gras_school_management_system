using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Application;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Application.Behaviors;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.UnitTests.Application;

/// <summary>
/// Tests the mediator wiring itself: handler discovery, behaviour ORDER, and the commands-only
/// transaction rule.
/// </summary>
/// <remarks>
/// These are the tests that would catch a broken pipeline. Every other test in the suite constructs its
/// handler directly and so proves nothing about whether validation, logging or transactions are
/// actually attached.
/// </remarks>
public sealed class PipelineTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Substitute.For<IUnitOfWork>());
        services.AddSingleton(Substitute.For<ISampleRecordRepository>());

        // TASK-0002: IScopeResolver (registered by AddApplication) depends on these two ports, which
        // Infrastructure implements. This container only wires the Application layer, so — same
        // treatment as IUnitOfWork/ISampleRecordRepository above — they are stubbed here for the
        // pipeline-wiring check alone.
        services.AddSingleton(Substitute.For<IPupilArmOfRecordLookup>());
        services.AddSingleton(Substitute.For<IResultSetArmLookup>());

        services.AddOptions<PipelineOptions>();

        services.AddApplication();

        // validateScopes catches a singleton capturing a scoped dependency — the classic DI bug that
        // works in tests and leaks a DbContext across requests in production.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    [Fact]
    public void EveryRequestInTheAssemblyResolvesAHandler()
    {
        // Handlers are discovered by assembly scanning, so a mistake shows up as a runtime "no handler
        // registered" on the first request. This turns that into a test failure instead.
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var requestTypes = ApplicationDependencyInjection.Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(@interface => @interface.IsGenericType &&
                                     @interface.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(@interface => (Request: type, Response: @interface.GetGenericArguments()[0])))
            .ToArray();

        requestTypes.ShouldNotBeEmpty("Expected to find requests to check.");

        var unresolvable = new List<string>();

        foreach (var (request, response) in requestTypes)
        {
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request, response);

            if (scope.ServiceProvider.GetService(handlerType) is null)
            {
                unresolvable.Add(request.Name);
            }
        }

        unresolvable.ShouldBeEmpty(
            $"No handler resolved for: {string.Join(", ", unresolvable)}");
    }

    [Fact]
    public void BehavioursForAQueryRunInTheDocumentedOrder()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var behaviours = scope.ServiceProvider
            .GetServices<IPipelineBehavior<PingQuery, Result<PingResponse>>>()
            .Select(behaviour => behaviour.GetType().GetGenericTypeDefinition())
            .ToArray();

        // The order documented in AddApplication. First registered is OUTERMOST. If this test fails
        // because you reordered the registrations, make sure you meant to: exception logging must wrap
        // everything, and validation must precede the handler.
        behaviours.ShouldBe(
        [
            typeof(UnhandledExceptionBehavior<,>),
            typeof(RequestLoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(PerformanceBehavior<,>),
        ]);
    }

    [Fact]
    public void AQueryNeverGetsTheUnitOfWorkBehaviour()
    {
        // THE test for the commands-only transaction rule. UnitOfWorkBehavior is constrained to
        // IBaseCommand, so the container cannot close it over a query type. This asserts that the
        // constraint actually has that effect rather than being decorative.
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var behaviours = scope.ServiceProvider
            .GetServices<IPipelineBehavior<PingQuery, Result<PingResponse>>>()
            .Select(behaviour => behaviour.GetType().GetGenericTypeDefinition())
            .ToArray();

        behaviours.ShouldNotContain(typeof(UnitOfWorkBehavior<,>));
    }

    [Fact]
    public void ACommandGetsTheUnitOfWorkBehaviourInnermost()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var behaviours = scope.ServiceProvider
            .GetServices<IPipelineBehavior<CreateSampleRecordCommand, Result<CreateSampleRecordResponse>>>()
            .Select(behaviour => behaviour.GetType().GetGenericTypeDefinition())
            .ToArray();

        behaviours.ShouldBe(
        [
            typeof(UnhandledExceptionBehavior<,>),
            typeof(RequestLoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(PerformanceBehavior<,>),
            typeof(UnitOfWorkBehavior<,>),
        ]);

        // Innermost, so the transaction is open for the shortest possible time.
        behaviours[^1].ShouldBe(typeof(UnitOfWorkBehavior<,>));
    }

    [Fact]
    public async Task AnInvalidRequestIsRejectedWithoutReachingTheHandler()
    {
        // End-to-end through the real pipeline: an empty name fails PingQueryValidator, so the behaviour
        // short-circuits with a ValidationError and the handler is never entered.
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(
            new PingQuery(string.Empty),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.ShouldBeOfType<ValidationError>();

        var validationError = (ValidationError)result.Error;
        validationError.Failures.ShouldContainKey(nameof(PingQuery.Name));
    }

    [Fact]
    public async Task AValidRequestReachesTheHandler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(
            new PingQuery("Ada"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Message.ShouldBe("Hello, Ada.");
    }

    [Fact]
    public async Task SendAsync_RejectsANullRequest()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sender.SendAsync<Result<PingResponse>>(null!, TestContext.Current.CancellationToken));
    }
}
