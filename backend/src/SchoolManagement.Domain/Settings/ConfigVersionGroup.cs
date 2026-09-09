namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Which settings group a <see cref="ConfigVersion"/> row was written for (spec 6.2.9). Stored as a
/// string (<c>ConfigVersionConfiguration.HasConversion&lt;string&gt;()</c>), so a future card adding
/// a member here is a code-only change — no migration required.
/// </summary>
/// <remarks>
/// TASK-0005b's uploads are also identity-group edits (they resave the same
/// <see cref="SchoolProfile"/> row) and reuse <see cref="Identity"/> rather than adding a member of
/// their own.
/// </remarks>
public enum ConfigVersionGroup
{
    /// <summary>A save through <c>PATCH /settings/identity</c>, or a logo/signature upload (TASK-0005b).</summary>
    Identity = 0,

    /// <summary>A save through <c>PATCH /settings/abbreviation</c> (TASK-0005c).</summary>
    Abbreviation = 1,

    /// <summary>A save through <c>PATCH /settings/reg-number</c> (TASK-0005c).</summary>
    RegistrationNumber = 2,
}
