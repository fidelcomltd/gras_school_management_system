using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// The response body of <c>GET /api/v1/settings</c>. TASK-0005b extends this same envelope
/// additively (logo/signature are read through <see cref="Identity"/>'s own follow-up serving
/// endpoints rather than a new top-level field here).
/// </summary>
/// <param name="Identity">The school identity group.</param>
/// <param name="Abbreviation">The registration-number abbreviation group.</param>
/// <param name="RegNumber">The registration-number pattern group.</param>
public sealed record SettingsDto(
    SettingsIdentityGroupDto Identity,
    SettingsAbbreviationGroupDto Abbreviation,
    SettingsRegNumberGroupDto RegNumber);

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

/// <summary>
/// The abbreviation group, both inside <see cref="SettingsDto"/> and as
/// <c>PATCH /api/v1/settings/abbreviation</c>'s own success body (spec 6.2.4).
/// </summary>
/// <param name="Abbreviation">2 to 8 characters. Frozen into every registration number issued from now on.</param>
/// <param name="IssuedCount">
/// How many issued registration numbers currently begin with <see cref="Abbreviation"/> — the count
/// spec 6.2.4's confirmation dialogue names before an admin types <c>CHANGE</c>.
/// <see langword="null"/> means "no register exists yet to count" (TASK-0005c ships this field
/// permanently <see langword="null"/>; wiring the real count is TASK-0051's). NEVER <c>0</c> — that
/// would claim zero pupils hold the abbreviation as a fact this card cannot support.
/// </param>
/// <param name="VersionNumber">
/// The abbreviation group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next <c>PATCH</c>.
/// </param>
public sealed record SettingsAbbreviationGroupDto(string Abbreviation, int? IssuedCount, int VersionNumber);

/// <summary>
/// The registration-number pattern group, both inside <see cref="SettingsDto"/> and as
/// <c>PATCH /api/v1/settings/reg-number</c>'s own success body (spec 6.2.4).
/// </summary>
/// <param name="Separator">One of <c>/</c>, <c>-</c>, <c>.</c>.</param>
/// <param name="SerialWidth">3 to 6. Serials are zero-padded to this width.</param>
/// <param name="SerialReset">Whether the serial restarts each admission year or runs continuously.</param>
/// <param name="YearSource">
/// Always <c>AdmissionYear</c> — fixed, spec 6.2.4: "a number that changes meaning with the calendar
/// is not an identifier." No <c>PATCH</c> can change it.
/// </param>
/// <param name="VersionNumber">
/// The reg-number group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next <c>PATCH</c>.
/// </param>
public sealed record SettingsRegNumberGroupDto(
    string Separator,
    int SerialWidth,
    RegNumberSerialReset SerialReset,
    string YearSource,
    int VersionNumber);
