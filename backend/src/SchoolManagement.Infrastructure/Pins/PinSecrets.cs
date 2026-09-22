using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Pins;

namespace SchoolManagement.Infrastructure.Pins;

/// <summary>Keys for <see cref="PinSecrets"/>, from configuration (environment variables on the VPS).</summary>
internal sealed class PinSecretsOptions
{
    public const string SectionName = "Pins";

    /// <summary>Base64 of 32 random bytes: the HMAC key for <c>lookup_key</c>.</summary>
    public string? LookupKey { get; set; }

    /// <summary>Base64 of 32 random bytes: the AES-256-GCM key for the reprint ciphertext.</summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Development and tests only: derive fixed, obviously non-production keys when none are configured. Set in
    /// <c>appsettings.Development.json</c> and the test host, never in production.
    /// </summary>
    public bool AllowDevelopmentKeys { get; set; }
}

/// <summary>Fails startup when the keys are missing or malformed and development keys are not allowed.</summary>
internal sealed class PinSecretsOptionsValidator : IValidateOptions<PinSecretsOptions>
{
    public ValidateOptionsResult Validate(string? name, PinSecretsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.AllowDevelopmentKeys && options.LookupKey is null && options.EncryptionKey is null)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (!IsKey(options.LookupKey))
        {
            failures.Add("Pins:LookupKey must be base64 of exactly 32 bytes.");
        }

        if (!IsKey(options.EncryptionKey))
        {
            failures.Add("Pins:EncryptionKey must be base64 of exactly 32 bytes.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[64];
        return Convert.TryFromBase64String(value, buffer, out var written) && written == 32;
    }
}

/// <summary>
/// Spec 6.8.6 cryptography. Argon2id uses a pin-specific cost (4 MB, 1 pass) rather than the password cost, because a
/// batch of 2,000 pins is hashed in one request and a pin carries ~50 bits of entropy behind a keyed lookup HMAC.
/// </summary>
internal sealed class PinSecrets : IPinSecrets
{
    private const int MemorySizeKiB = 4096;
    private const int Iterations = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _lookupKey;
    private readonly byte[] _encryptionKey;

    public PinSecrets(IOptions<PinSecretsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Value;
        _lookupKey = configured.LookupKey is { } lookup ? Convert.FromBase64String(lookup) : DevelopmentKey("lookup");
        _encryptionKey = configured.EncryptionKey is { } encryption ? Convert.FromBase64String(encryption) : DevelopmentKey("encryption");
    }

    public PinSecretMaterial Protect(string pinValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(pinValue);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Hash(pinValue, salt);
        var encoded = string.Join(
            '$',
            "argon2id",
            "v=19",
            string.Create(CultureInfo.InvariantCulture, $"m={MemorySizeKiB},t={Iterations},p=1"),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
        return new PinSecretMaterial(encoded, LookupKeyFor(pinValue), Encrypt(pinValue));
    }

    public string LookupKeyFor(string pinValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(pinValue);
        return Convert.ToHexStringLower(HMACSHA256.HashData(_lookupKey, Encoding.UTF8.GetBytes(pinValue)));
    }

    public bool Verify(string pinValue, string pinHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(pinValue);
        ArgumentException.ThrowIfNullOrEmpty(pinHash);
        var segments = pinHash.Split('$');
        if (segments.Length != 5 || segments[0] != "argon2id")
        {
            return false;
        }

        var expected = Convert.FromBase64String(segments[4]);
        var computed = Hash(pinValue, Convert.FromBase64String(segments[3]));
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    public string Reveal(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);
        var bytes = Convert.FromBase64String(ciphertext);
        var nonce = bytes.AsSpan(0, NonceSize);
        var tag = bytes.AsSpan(NonceSize, TagSize);
        var cipher = bytes.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private static byte[] Hash(string pinValue, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(pinValue))
        {
            Salt = salt,
            MemorySize = MemorySizeKiB,
            Iterations = Iterations,
            DegreeOfParallelism = 1,
        };
        return argon2.GetBytes(HashSize);
    }

    private string Encrypt(string pinValue)
    {
        var plain = Encoding.UTF8.GetBytes(pinValue);
        var output = new byte[NonceSize + TagSize + plain.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return Convert.ToBase64String(output);
    }

    private static byte[] DevelopmentKey(string purpose) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("schoolmanagement-development-only-pin-" + purpose + "-key-never-production"));
}
