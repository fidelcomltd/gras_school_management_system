using SchoolManagement.Domain.Common;

namespace SchoolManagement.UnitTests.Domain;

/// <summary>
/// Tests for the outcome type every handler returns. Small type, high leverage — every error path in
/// the application flows through it.
/// </summary>
public sealed class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_CarriesTheError()
    {
        var error = Error.NotFound("thing.not_found", "No such thing.");

        var result = Result.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void TypedSuccess_ExposesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void ReadingValueOfAFailure_Throws()
    {
        // The whole point of the type: silently returning default would let a null or zero flow into
        // business logic as if it were real data. Loud is correct here.
        var result = Result.Failure<int>(Error.Failure("boom", "Something broke."));

        var exception = Should.Throw<InvalidOperationException>(() => _ = result.Value);

        // The message must name the error code, or a production stack trace tells you nothing about
        // which failure was mishandled.
        exception.Message.ShouldContain("boom");
    }

    [Fact]
    public void TryGetValue_DoesNotThrowOnFailure()
    {
        var result = Result.Failure<string>(Error.Conflict("c", "Conflict."));

        var succeeded = result.TryGetValue(out var value);

        succeeded.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void TryGetValue_ReturnsValueOnSuccess()
    {
        var result = Result.Success("payload");

        var succeeded = result.TryGetValue(out var value);

        succeeded.ShouldBeTrue();
        value.ShouldBe("payload");
    }

    [Fact]
    public void ConstructingASuccessWithAnError_Throws()
    {
        // Guards the invariant. Without it, a Result could claim success while carrying a failure, and
        // callers checking IsSuccess would take the happy path with an error in hand.
        Should.Throw<ArgumentException>(() => new ProbeResult(isSuccess: true, Error.Failure("x", "y")));
    }

    [Fact]
    public void ConstructingAFailureWithoutAnError_Throws()
    {
        Should.Throw<ArgumentException>(() => new ProbeResult(isSuccess: false, Error.None));
    }

    /// <summary>Exposes the protected constructor so the invariant can be tested directly.</summary>
    private sealed class ProbeResult(bool isSuccess, Error error) : Result(isSuccess, error);
}
