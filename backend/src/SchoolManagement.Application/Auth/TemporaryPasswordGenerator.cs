using System.Security.Cryptography;
using SchoolManagement.Domain.Auth;

namespace SchoolManagement.Application.Auth;

/// <summary>
/// Generates a random temporary password for the bootstrap seam (spec 6.1.6) and an admin's creation or forced reset.
/// Digits only (project lead, 2026-09-30): it is read out or copied by hand to the new admin, and a string of digits
/// survives that where mixed-case letters did not. Spec 6.1.11's letter rule is not needed here: a temporary password
/// only signs in to the change-password screen, and the password the admin chooses there meets the full rule.
/// </summary>
public static class TemporaryPasswordGenerator
{
    /// <summary>Length of a generated password: exactly <see cref="AuthPolicy.PasswordMinLength"/>, about 40 bits, behind lockout.</summary>
    public const int GeneratedLength = AuthPolicy.PasswordMinLength;

    /// <summary>Generates <see cref="GeneratedLength"/> uniformly random digits.</summary>
    public static string Generate()
    {
        var characters = new char[GeneratedLength];
        for (var index = 0; index < GeneratedLength; index++)
        {
            characters[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(characters);
    }
}
