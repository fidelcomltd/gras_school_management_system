using System.Diagnostics;
using Microsoft.Extensions.Options;
using SchoolManagement.Infrastructure.Auth;

namespace SchoolManagement.UnitTests.Infrastructure.Auth;

/// <summary>
/// Tests <see cref="Argon2idPasswordHasher"/> (spec 9.1). Uses the SMALLEST valid cost parameters —
/// the real production parameters (150-300ms) would make this suite unbearably slow; correctness of
/// the algorithm and the encoding does not depend on the cost.
/// </summary>
public sealed class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _hasher = new(Options.Create(new Argon2Options
    {
        MemorySizeKiB = 8192,
        Iterations = 1,
        DegreeOfParallelism = 1,
        SaltSizeBytes = 16,
        HashSizeBytes = 32,
    }));

    [Fact]
    public void Hash_ThenVerify_WithTheSamePassword_Succeeds()
    {
        var encoded = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("correct horse battery staple", encoded).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WithAWrongPassword_Fails()
    {
        var encoded = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("wrong password entirely", encoded).ShouldBeFalse();
    }

    [Fact]
    public void Hash_ProducesADifferentEncodedStringEachTime_BecauseTheSaltIsRandom()
    {
        var first = _hasher.Hash("the same password");
        var second = _hasher.Hash("the same password");

        first.ShouldNotBe(second);
        _hasher.Verify("the same password", first).ShouldBeTrue();
        _hasher.Verify("the same password", second).ShouldBeTrue();
    }

    [Fact]
    public void Hash_EmbedsTheConfiguredCostParameters()
    {
        var encoded = _hasher.Hash("a password");

        encoded.ShouldStartWith("argon2id$v=19$m=8192,t=1,p=1$");
    }

    [Theory]
    [InlineData("not-even-argon2-shaped")]
    [InlineData("argon2id$v=19$m=8192,t=1,p=1$onlyonepart")]
    [InlineData("argon2id$v=19$m=notanumber,t=1,p=1$c2FsdA==$aGFzaA==")]
    [InlineData("")]
    public void Verify_WithAMalformedEncodedHash_ReturnsFalseRatherThanThrowing(string malformed)
    {
        if (malformed.Length == 0)
        {
            // Guarded separately: the public contract throws on an empty encoded hash (a programming
            // error, never a value that legitimately reached this far), not a graceful false.
            Should.Throw<ArgumentException>(() => _hasher.Verify("anything", malformed));
            return;
        }

        _hasher.Verify("anything", malformed).ShouldBeFalse();
    }

    [Fact]
    public void DummyHashForTimingParity_IsCachedAndEmbedsTheConfiguredCostParameters()
    {
        var first = _hasher.DummyHashForTimingParity;
        var second = _hasher.DummyHashForTimingParity;

        first.ShouldBe(second, "must be computed once and cached — recomputing it on every failed sign-in would itself be a timing tell.");
        first.ShouldStartWith("argon2id$v=19$m=8192,t=1,p=1$");
    }

    [Fact]
    public void Verify_AgainstTheGenuineDummyHash_TakesMeasurablyLongerThanAgainstAMalformedOne()
    {
        // Second-pass review MEDIUM 6: the previous version of this test asserted only
        // Verify(...).ShouldBeFalse() — which a MALFORMED hash also satisfies without running any KDF
        // at all. Corrupting the dummy hash's cost parameters (e.g. "m=notanumber") would have stayed
        // green while silently turning the timing defence into a fast, computation-free no-op — the
        // exact class of regression HIGH 2 fixed. This asserts the thing that actually matters: a
        // genuine, parseable hash forces the full Argon2id computation and takes measurably longer
        // than a malformed one that fails to parse. Heavier cost parameters than the class default
        // widen the gap so the assertion is not noise-sensitive.
        var heavyHasher = new Argon2idPasswordHasher(Options.Create(new Argon2Options
        {
            MemorySizeKiB = 65_536,
            Iterations = 3,
            DegreeOfParallelism = 1,
            SaltSizeBytes = 16,
            HashSizeBytes = 32,
        }));

        const string malformed = "argon2id$v=19$m=notanumber,t=1,p=1$c2FsdA==$aGFzaA==";
        var genuine = heavyHasher.DummyHashForTimingParity;

        var malformedElapsed = Time(() => heavyHasher.Verify("anything", malformed).ShouldBeFalse());
        var genuineElapsed = Time(() => heavyHasher.Verify("anything", genuine).ShouldBeFalse());

        genuineElapsed.ShouldBeGreaterThan(
            malformedElapsed * 5,
            $"Verifying against a genuine (if dummy) hash took {genuineElapsed} versus {malformedElapsed} " +
            "for a malformed one that never reaches the KDF at all — the gap should be large. If this " +
            "ever fails, the dummy-hash timing defence has likely become a no-op.");
    }

    private static TimeSpan Time(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        return stopwatch.Elapsed;
    }
}
