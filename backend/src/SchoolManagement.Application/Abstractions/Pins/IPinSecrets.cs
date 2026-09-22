namespace SchoolManagement.Application.Abstractions.Pins;

/// <summary>The three stored forms of one pin value (spec 6.8.5, 6.8.6).</summary>
/// <param name="PinHash">Argon2id, self-describing: the record of truth.</param>
/// <param name="LookupKey">Keyed HMAC-SHA-256 hex, indexed and unique, so validation finds the row from the value alone.</param>
/// <param name="Ciphertext">AES-256-GCM as base64 (nonce, tag, cipher), for reprinting until the purge date.</param>
public sealed record PinSecretMaterial(string PinHash, string LookupKey, string Ciphertext);

/// <summary>
/// The pin cryptography (spec 6.8.6). Both keys live outside the database, in configuration on the VPS. The
/// implementation is Infrastructure's, and values passed in are already normalised.
/// </summary>
public interface IPinSecrets
{
    /// <summary>Computes all three stored forms of a new pin. CPU-bound (Argon2id); safe to call in parallel.</summary>
    PinSecretMaterial Protect(string pinValue);

    /// <summary>The lookup key for a value a parent typed. Cheap.</summary>
    string LookupKeyFor(string pinValue);

    /// <summary>Verifies a value against a stored Argon2id hash.</summary>
    bool Verify(string pinValue, string pinHash);

    /// <summary>Decrypts a stored ciphertext for printing.</summary>
    string Reveal(string ciphertext);
}
