namespace SchoolManagement.Domain.Common;

/// <summary>
/// Outcome of an operation that can fail in an expected way.
/// </summary>
/// <remarks>
/// <para>
/// THE RULE: handlers return <see cref="Result"/> or <see cref="Result{TValue}"/>; they do not
/// throw for outcomes you can anticipate (not found, conflict, forbidden, invalid input).
/// Exceptions are reserved for genuine defects and infrastructure faults, which the
/// unhandled-exception behaviour and the global exception handler turn into a 500.
/// </para>
/// <para>
/// Why: an expected failure that travels as an exception is invisible in the method
/// signature, costs a stack unwind on a routine path, and tempts callers into
/// <c>catch</c> blocks that swallow real bugs. See <c>docs/adr/0004-error-model.md</c>.
/// </para>
/// </remarks>
public class Result
{
    /// <summary>Initialises a result, guarding the success/error invariant.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when a success carries an error, or a failure carries none. This is a
    /// programming error, not an expected failure, so it throws rather than returning.
    /// </exception>
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        switch (isSuccess)
        {
            case true when error != Error.None:
                throw new ArgumentException("A successful result cannot carry an error.", nameof(error));
            case false when error == Error.None:
                throw new ArgumentException("A failed result must carry an error.", nameof(error));
            default:
                break;
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed. Convenience inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The failure. Equals <see cref="Error.None"/> when <see cref="IsSuccess"/> is true.
    /// </summary>
    public Error Error { get; }

    /// <summary>Creates a success with no value (for commands that return nothing).</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a failure.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a success carrying <paramref name="value"/>.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>Creates a typed failure.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// Outcome of an operation that returns <typeparamref name="TValue"/> on success.
/// </summary>
/// <typeparam name="TValue">The success payload. Always a DTO, never a domain entity.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>Initialises a typed result. Use <see cref="Result.Success{TValue}"/> instead.</summary>
    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The success payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is a failure. Check <see cref="Result.IsSuccess"/> first, or use
    /// <see cref="TryGetValue"/>.
    /// </exception>
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations.
    // Justified: reading Value on a failed result is a programming error that must be loud.
    // Returning default would silently propagate a null/zero into business logic, which is
    // exactly the class of bug this type exists to prevent. TryGetValue is the safe accessor,
    // and the analyser cannot see that the property is guarded by IsSuccess.
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read {nameof(Value)} of a failed result. Error code: '{Error.Code}'.");
#pragma warning restore CA1065

    /// <summary>
    /// Non-throwing accessor for the payload.
    /// </summary>
    /// <param name="value">The payload when this result is a success; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> when this result is a success.</returns>
    public bool TryGetValue(out TValue? value)
    {
        value = IsSuccess ? _value : default;
        return IsSuccess;
    }
}
