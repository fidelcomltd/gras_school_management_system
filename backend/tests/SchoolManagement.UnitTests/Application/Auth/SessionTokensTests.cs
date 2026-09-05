using SchoolManagement.Application.Auth;

namespace SchoolManagement.UnitTests.Application.Auth;

/// <summary>Tests <see cref="SessionTokens"/> (spec 9.1's 32-byte CSPRNG token, hashed for storage).</summary>
public sealed class SessionTokensTests
{
    [Fact]
    public void GenerateRawToken_ProducesADifferentValueEachTime()
    {
        var first = SessionTokens.GenerateRawToken();
        var second = SessionTokens.GenerateRawToken();

        first.ShouldNotBe(second);
    }

    [Fact]
    public void GenerateRawToken_IsUrlSafe()
    {
        var token = SessionTokens.GenerateRawToken();

        token.ShouldNotContain("+");
        token.ShouldNotContain("/");
        token.ShouldNotContain("=");
    }

    [Fact]
    public void GenerateRawToken_DecodesToExactly32Bytes()
    {
        var token = SessionTokens.GenerateRawToken();

        var padded = token.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - (padded.Length % 4)) % 4);

        Convert.FromBase64String(padded).Length.ShouldBe(SessionTokens.TokenLengthBytes);
    }

    [Fact]
    public void HashToken_IsDeterministic()
    {
        var raw = SessionTokens.GenerateRawToken();

        SessionTokens.HashToken(raw).ShouldBe(SessionTokens.HashToken(raw));
    }

    [Fact]
    public void HashToken_DiffersForDifferentTokens()
    {
        var first = SessionTokens.GenerateRawToken();
        var second = SessionTokens.GenerateRawToken();

        SessionTokens.HashToken(first).ShouldNotBe(SessionTokens.HashToken(second));
    }

    [Fact]
    public void HashToken_NeverContainsTheRawTokenValue()
    {
        var raw = SessionTokens.GenerateRawToken();

        SessionTokens.HashToken(raw).ShouldNotContain(raw);
    }

    [Fact]
    public void HashToken_IsLowercaseHex()
    {
        var hash = SessionTokens.HashToken(SessionTokens.GenerateRawToken());

        hash.Length.ShouldBe(64); // SHA-256 = 32 bytes = 64 hex characters.
        hash.ShouldMatch("^[0-9a-f]{64}$");
    }
}
