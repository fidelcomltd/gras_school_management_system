namespace SchoolManagement.Application.Settings;

/// <summary>
/// The response body of <c>GET /api/v1/settings</c>. Only <see cref="Identity"/> exists as of
/// TASK-0005a; TASK-0005b and TASK-0005c extend this same envelope additively with sibling groups
/// (logo/signature are read through <see cref="Identity"/>'s own follow-up serving endpoints rather
/// than a new top-level field, and registration-number/abbreviation get their own group here).
/// </summary>
/// <param name="Identity">The school identity group.</param>
public sealed record SettingsDto(SettingsIdentityGroupDto Identity);

/// <summary>
/// The school identity group, both inside <see cref="SettingsDto"/> and as
/// <c>PATCH /api/v1/settings/identity</c>'s own success body (spec 6.2.3).
/// </summary>
/// <param name="SchoolName">Full school name. Appears in full on the result sheet header.</param>
/// <param name="ShortName">Used where the full name will not fit, for example the pin slip.</param>
/// <param name="Address">Multi-line permitted.</param>
/// <param name="Phone">Nigerian format, normalised to <c>+234</c> form.</param>
/// <param name="Email">Valid email format, stored lower-invariant.</param>
/// <param name="Motto"><see langword="null"/> when unset. Printed under the school name if present.</param>
/// <param name="HeadTeacherName">Printed above the head teacher's signature block.</param>
/// <param name="Timezone">Always <c>Africa/Lagos</c>. Fixed; a <c>PATCH</c> cannot change it.</param>
/// <param name="VersionNumber">
/// The identity group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next <c>PATCH</c>.
/// </param>
public sealed record SettingsIdentityGroupDto(
    string SchoolName,
    string ShortName,
    string Address,
    string Phone,
    string Email,
    string? Motto,
    string HeadTeacherName,
    string Timezone,
    int VersionNumber);
