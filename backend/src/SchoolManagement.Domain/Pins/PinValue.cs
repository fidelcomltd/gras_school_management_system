using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.Domain.Pins;

/// <summary>
/// Spec 6.8.6's character set and formatting. The alphabet leaves out I, L, O, 0 and 1, which a parent misreads
/// from a printed slip. Entry is uppercased and stripped of spaces and hyphens, so any phone keyboard works.
/// </summary>
public static class PinValue
{
    /// <summary>The 31 generated characters.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>A new random value of <paramref name="length"/> characters, from a cryptographic source.</summary>
    public static string Generate(int length)
    {
        var characters = new char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(characters);
    }

    /// <summary>What a parent typed, uppercased, with spaces and hyphens removed.</summary>
    public static string Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new StringBuilder(input.Length);
        foreach (var character in input)
        {
            if (character is ' ' or '-' or '\t')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    /// <summary>Printed grouped in fives with a hyphen, e.g. <c>H7K2M-QRW4T</c>.</summary>
    public static string Format(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length + (value.Length / 5));
        for (var index = 0; index < value.Length; index++)
        {
            if (index > 0 && index % 5 == 0)
            {
                builder.Append('-');
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }
}
