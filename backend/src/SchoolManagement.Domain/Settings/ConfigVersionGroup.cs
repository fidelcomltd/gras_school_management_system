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

    /// <summary>A save through <c>PUT /settings/grading</c> or <c>POST /settings/grading/reset</c> (TASK-0069).</summary>
    Grading = 3,

    /// <summary>A save through <c>PUT /settings/assessment</c> (TASK-0069).</summary>
    Assessment = 4,

    /// <summary>A save through <c>PUT /settings/result-rules</c> (TASK-0077).</summary>
    ResultRules = 5,

    /// <summary>A save through <c>PUT /settings/rating-scales</c> (TASK-0072 stage 1).</summary>
    RatingScales = 6,

    /// <summary>A save through <c>PUT /settings/development-domains</c> (TASK-0072 stage 2b).</summary>
    DevelopmentDomains = 7,
}
