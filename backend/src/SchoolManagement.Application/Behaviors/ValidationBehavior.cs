using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Behaviors;

/// <summary>
/// PIPELINE STAGE 3. Runs every registered validator for the request, aggregates the failures,
/// and short-circuits with a <see cref="ValidationError"/> without calling the handler.
/// </summary>
/// <remarks>
/// <para>
/// WHY VALIDATION LIVES HERE AND NOT IN AN ENDPOINT FILTER: an endpoint filter has to be attached
/// per endpoint, so the failure mode is "somebody adds an endpoint and forgets", which produces an
/// unvalidated endpoint that looks completely normal in review. As a pipeline behaviour it is
/// structural — every request dispatched through <see cref="ISender"/> passes through it and there
/// is no way to opt out. This is a deliberate deviation from the root CLAUDE.md §6 wording
/// ("registered as an endpoint filter"), taken because it satisfies that rule's intent
/// (validation executes before the handler) with a stronger guarantee. Recorded in
/// <c>docs/ASSUMPTIONS.md</c> and <c>docs/adr/0005-validation-placement.md</c>.
/// </para>
/// <para>
/// ALL validators run and ALL failures are returned together — a client fixing a form should not
/// have to make five round trips to discover five bad fields. "Fail fast" here means the handler
/// is never entered, not that validation stops at the first error.
/// </para>
/// <para>
/// If a request has no validator, this stage passes it through. Requiring one at runtime would
/// turn a missing validator into a production 500; instead
/// <c>ValidatorCoverageTests.EveryRequestHasAValidator</c> fails the BUILD, which is where that
/// mistake should be caught.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    // Built once per closed generic type by the runtime, so the reflection below is paid once
    // per request type per process rather than on every request.
    private static readonly Func<ValidationError, TResponse> CreateFailure = BuildFailureFactory();

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        PipelineContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var applicable = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (applicable.Length == 0)
        {
            return await continuation(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            applicable.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .ToArray();

        if (failures.Length == 0)
        {
            return await continuation(cancellationToken);
        }

        var failuresByProperty = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage)
                              .Distinct(StringComparer.Ordinal)
                              .ToArray(),
                StringComparer.Ordinal);

        BehaviorLog.ValidationFailed(logger, typeof(TRequest).Name, failuresByProperty.Count);

        return CreateFailure(new ValidationError(failuresByProperty));
    }

    /// <summary>
    /// Produces a factory that builds a failed <typeparamref name="TResponse"/>, whether that is a
    /// bare <see cref="Result"/> or a <see cref="Result{TValue}"/>.
    /// </summary>
    private static Func<ValidationError, TResponse> BuildFailureFactory()
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return static error => (TResponse)(object)Result.Failure(error);
        }

        if (!typeof(TResponse).IsGenericType ||
            typeof(TResponse).GetGenericTypeDefinition() != typeof(Result<>))
        {
            throw new InvalidOperationException(
                $"'{typeof(TResponse)}' is not a supported response type. A request must return " +
                $"either {nameof(Result)} or Result<T> so the pipeline can construct a failure " +
                "for it without the handler running.");
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];

        var failureMethod = typeof(Result)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method is { Name: nameof(Result.Failure), IsGenericMethodDefinition: true })
            .MakeGenericMethod(valueType);

        return error => (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
