using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Application;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Persistence;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Security;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Behaviors;
using SchoolManagement.Application.Reference.Ping;
using SchoolManagement.Application.Reference.SampleRecords;
using SchoolManagement.Application.Settings;
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

        // TASK-0003: the Auth/* handlers depend on these Application abstractions, all implemented
        // by Infrastructure (or the Api layer, for the two Identity ports) — same treatment as the
        // TASK-0002 stubs above, needed only so this Application-only container can construct them.
        services.AddSingleton(Substitute.For<IEffectivePrivilegeProvider>());
        services.AddSingleton(Substitute.For<IAdminAccountRepository>());
        services.AddSingleton(Substitute.For<IAdminSessionRepository>());
        services.AddSingleton(Substitute.For<IPasswordHasher>());
        services.AddSingleton(Substitute.For<ICurrentUser>());
        services.AddSingleton(Substitute.For<ICurrentSession>());

        // TASK-0005a: the Settings/* handlers depend on these ports, implemented by Infrastructure —
        // same treatment as every other repository stubbed above, needed only so this Application-only
        // container can construct them.
        services.AddSingleton(Substitute.For<ISchoolProfileRepository>());
        services.AddSingleton(Substitute.For<IConfigVersionRepository>());
        services.AddSingleton(Substitute.For<ISystemAuditSink>());

        // TASK-0005c: the reg-number handlers depend on this port too, implemented by Infrastructure —
        // same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IRegistrationCounterRepository>());

        // TASK-0069: the grading/assessment handlers (plus every other Settings/* handler, since the
        // snapshot now carries both groups regardless of which one changed) depend on these four
        // ports, implemented by Infrastructure — same treatment as every other repository stubbed
        // above.
        services.AddSingleton(Substitute.For<IGradingBandRepository>());
        services.AddSingleton(Substitute.For<IAssessmentComponentRepository>());
        services.AddSingleton(Substitute.For<ISubjectScoreSessionLockLookup>());
        services.AddSingleton(Substitute.For<IPublishedResultsGate>());

        // TASK-0077: the result-rules handlers (plus every other Settings/* handler, since the
        // snapshot now carries all three groups regardless of which one changed) depend on this port,
        // implemented by Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IResultRulesRepository>());

        // TASK-0028 dispatch 2: the Roles/* handlers depend on this port, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IRoleRepository>());
        services.AddSingleton(Substitute.For<IRoleAssignmentRepository>());

        // TASK-0035: the Sessions/* handlers depend on these two ports, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IAcademicSessionRepository>());
        services.AddSingleton(Substitute.For<ITermRepository>());

        // TASK-0076 dispatch A: CloseTermHandler depends on this port for spec 6.3.6's result-set
        // precondition, implemented by Infrastructure — same treatment as every other repository
        // stubbed above.
        services.AddSingleton(Substitute.For<IResultSetRepository>());

        // TASK-0076 dispatch B: the score-sheet handlers (Get/Save/Void) depend on this port,
        // implemented by Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<ISubjectScoreRepository>());

        // TASK-0071: ComputeResultSetHandler depends on this port for the three computed tables,
        // implemented by Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IResultComputationRepository>());

        // TASK-0083 stage 1: the trait-rating handlers (Get/Save) depend on this port, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<ITraitRatingRepository>());

        // TASK-0083 stage 2: the development-rating handlers (Get/Save) depend on this port,
        // implemented by Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IDevelopmentRatingRepository>());

        // TASK-0086 stage A: the attendance handlers (Get/Save) AND UpdateTermHandler (delta item 5)
        // depend on this port; the two remark-sheet handler families depend on the other — both
        // implemented by Infrastructure, same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IAttendanceEntryRepository>());
        services.AddSingleton(Substitute.For<IPupilRemarkRepository>());

        // TASK-0086 stage B: the remark-template handlers (Get/Create/Delete) depend on this port,
        // implemented by Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IRemarkTemplateRepository>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Weekly.IWeeklyReportRepository>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Weekly.IWeeklyNameLookup>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Pupils.IPupilRecordRepository>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Weekly.IWeeklySheetPdfRenderer>());

        // TASK-0038: the Classes/* handlers depend on these two ports, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<ISectionRepository>());
        services.AddSingleton(Substitute.For<IClassLevelRepository>());

        // TASK-0072 stages 1/2b: the rating-scales and development-domains handlers (plus every other
        // Settings/* handler, since the snapshot now carries both groups regardless of which one
        // changed) depend on these four ports, implemented by Infrastructure — same treatment as every
        // other repository/gate stubbed above. Missing until now: stage 1 never added them here, which
        // is exactly the gap this card's STEP 1 closes (decisions/2026-Q3.md, stage 2a closure note).
        services.AddSingleton(Substitute.For<IRatingScaleRepository>());
        services.AddSingleton(Substitute.For<IRatingScaleUsageGate>());
        services.AddSingleton(Substitute.For<IDevelopmentDomainRepository>());
        services.AddSingleton(Substitute.For<IDevelopmentIndicatorUsageGate>());

        // TASK-0072 stage 3a: every Settings/* handler now asks ISettingsSnapshotSource for the whole
        // config_version snapshot input in one call, instead of injecting one repository per OTHER
        // group itself — same treatment as every other port stubbed above.
        services.AddSingleton(Substitute.For<ISettingsSnapshotSource>());

        // TASK-0072 stage 3b: the traits handler depends on these two ports, implemented by
        // Infrastructure — same treatment as every other repository/gate stubbed above.
        services.AddSingleton(Substitute.For<ITraitRepository>());
        services.AddSingleton(Substitute.For<ITraitUsageGate>());

        // TASK-0039: the Arms/* handlers (plus UpdateSessionHandler/GetSessionHandler's new arm-count
        // read) depend on this port, implemented by Infrastructure — same treatment as every other
        // repository stubbed above.
        services.AddSingleton(Substitute.For<IArmRepository>());

        // TASK-0050: the Pupils/* handlers depend on this port, implemented by Infrastructure — same
        // treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IPupilRepository>());

        // TASK-0062: CreatePupilHandler and UpdateAdmissionRecordHandler depend on this port,
        // implemented by Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IAdmissionRecordRepository>());

        // TASK-0051: ApproveAdmissionCommandHandler depends on these two ports, implemented by
        // Infrastructure — same treatment as every other repository/port stubbed above.
        services.AddSingleton(Substitute.For<IEnrolmentRepository>());
        services.AddSingleton(Substitute.For<IPersistenceErrorTranslator>());

        // TASK-0063: CorrectRegistrationNumberHandler depends on this port, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IPupilRegNumberHistoryRepository>());

        // TASK-0049: the Audit/* read-surface handlers depend on this port, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        services.AddSingleton(Substitute.For<IAuditEventQueryRepository>());

        // TASK-0070: the Subjects/* handlers depend on these four ports, implemented by
        // Infrastructure — same treatment as every other repository stubbed above.
        // ISubjectMappingMarkLookup is the spec 6.6.6 mark check's seam, not a repository.
        services.AddSingleton(Substitute.For<ISubjectRepository>());
        services.AddSingleton(Substitute.For<ISubjectMappingRepository>());
        services.AddSingleton(Substitute.For<ISubjectMappingExceptionRepository>());
        services.AddSingleton(Substitute.For<ISubjectMappingMarkLookup>());

        // TASK-0005b stage B2: the upload handlers depend on these three ports, implemented by
        // Infrastructure — same treatment as every other repository/processor stubbed above.
        services.AddSingleton(Substitute.For<ISchoolImageRepository>());
        services.AddSingleton(Substitute.For<ISchoolImageProcessor>());
        services.AddSingleton(Substitute.For<ISchoolImageStore>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Pins.IPinBatchRepository>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Pins.IPinSecrets>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Pins.IPinSlipRenderer>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Portal.IPortalRepository>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Results.IResultSheetReader>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Results.IResultVerificationReader>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Results.IResultSheetPdfRenderer>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Results.IResultPdfCache>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Results.IAnnualResultRepository>());
        services.AddSingleton(Substitute.For<SchoolManagement.Application.Abstractions.Results.IAnnualSheetReader>());

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
