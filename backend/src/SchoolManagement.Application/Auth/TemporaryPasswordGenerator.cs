using System.Security.Cryptography;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Application.Auth;

/// <summary>
/// Generates a random temporary password satisfying spec 6.1.11's composition rule (minimum length,
/// at least one letter and one digit), for the bootstrap seam (spec 6.1.6) and — later, TASK-0019 —
/// a forced admin password reset.
/// </summary>
public static class TemporaryPasswordGenerator
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string AllCharacters = Letters + Digits;

    /// <summary>Length of a generated password. Comfortably above <see cref="AuthPolicy.PasswordMinLength"/>.</summary>
    public const int GeneratedLength = 20;

    /// <summary>
    /// Generates a password of <see cref="GeneratedLength"/> characters, guaranteed to contain at
    /// least one letter and one digit by construction rather than by chance.
    /// </summary>
    public static string Generate()
    {
        var characters = new char[GeneratedLength];

        // Guarantee composition first (fixed positions 0 and 1), then fill the rest uniformly, then
        // shuffle — so the guaranteed characters do not always land in the same two positions.
        characters[0] = PickFrom(Letters);
        characters[1] = PickFrom(Digits);

        for (var index = 2; index < GeneratedLength; index++)
        {
            characters[index] = PickFrom(AllCharacters);
        }

        Shuffle(characters);

        return new string(characters);
    }

    private static char PickFrom(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void Shuffle(char[] characters)
    {
        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }
    }
}
