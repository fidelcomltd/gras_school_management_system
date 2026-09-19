using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// The school identity singleton (spec 6.2.3), plus the abbreviation and the two independent
/// optimistic-concurrency pointers TASK-0005a's approved delta puts on this one row.
/// </summary>
/// <remarks>
/// <para>
/// A SINGLETON BY CONVENTION, not by any database mechanism: exactly one row, at
/// <see cref="SingletonId"/>, created once by the migration's seed data (<c>HasData</c> in
/// <c>SchoolProfileConfiguration</c>) and never inserted again. There is no "create" endpoint or
/// factory method — only <see cref="UpdateIdentity"/>, because the row always already exists.
/// </para>
/// <para>
/// <see cref="Abbreviation"/> and <see cref="AbbreviationVersionNumber"/> are SEEDED here (6.2.2:
/// <c>GRAS</c>) and physically live on this row, but this card's <see cref="UpdateIdentity"/> never
/// touches either — TASK-0005c owns <c>PATCH /settings/abbreviation</c> and its own domain method.
/// Two independent version-number columns on one row, rather than a second singleton table, is the
/// approved delta's explicit choice: an identity edit and an abbreviation edit must never spuriously
/// conflict each other's optimistic-concurrency check.
/// </para>
/// <para>
/// <see cref="Timezone"/> is fixed at <see cref="FixedTimezone"/> (spec 6.2.3: "Not editable in this
/// version") and carries no setter reachable from <see cref="UpdateIdentity"/> — a client cannot
/// change it because the command that could is never bound to one, and the API's global
/// <c>UnmappedMemberHandling.Disallow</c> rejects an attempt to smuggle it into the request body with
/// a 400 rather than silently ignoring it.
/// </para>
/// </remarks>
public sealed class SchoolProfile : Entity<Guid>
{
    /// <summary>The one row this entity ever has.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>6.2.2: the abbreviation the migration seeds.</summary>
    public const string SeededAbbreviation = "GRAS";

    /// <summary>6.2.3: "Not editable in this version."</summary>
    public const string FixedTimezone = "Africa/Lagos";

    /// <summary>6.2.3: <c>school_name</c>, String 160.</summary>
    public const int SchoolNameMaxLength = 160;

    /// <summary>6.2.3: <c>short_name</c>, String 60.</summary>
    public const int ShortNameMaxLength = 60;

    /// <summary>6.2.3: <c>abbreviation</c>, String 8 (2 to 8 characters — the floor is TASK-0005c's, this is the ceiling).</summary>
    public const int AbbreviationMaxLength = 8;

    /// <summary>TASK-0005c's own floor for <see cref="Abbreviation"/> — see <see cref="AbbreviationMaxLength"/>'s remark.</summary>
    public const int AbbreviationMinLength = 2;

    /// <summary>6.2.4: fixed, not editable — "a number that changes meaning with the calendar is not an identifier."</summary>
    public const string YearSource = "AdmissionYear";

    /// <summary>6.2.4: <c>separator</c> default.</summary>
    public const string DefaultSeparator = "/";

    /// <summary>6.2.4: <c>serial_width</c> default.</summary>
    public const int DefaultSerialWidth = 4;

    /// <summary>6.2.4: <c>serial_reset</c> default.</summary>
    public const RegNumberSerialReset DefaultSerialReset = RegNumberSerialReset.PerYear;

    /// <summary>6.2.3: <c>address</c>, String 300.</summary>
    public const int AddressMaxLength = 300;

    /// <summary>6.2.3: <c>phone</c>, String 20.</summary>
    public const int PhoneMaxLength = 20;

    /// <summary>6.2.3: <c>email</c>, String 160.</summary>
    public const int EmailMaxLength = 160;

    /// <summary>6.2.3: <c>motto</c>, String 120.</summary>
    public const int MottoMaxLength = 120;

    /// <summary>6.2.3: <c>head_teacher_name</c>, String 120.</summary>
    public const int HeadTeacherNameMaxLength = 120;

    // EF Core materialisation constructor. Every field is set from a database row (or migration seed
    // data) immediately afterward — there is no code path that leaves these placeholders live.
    private SchoolProfile()
        : base()
    {
        SchoolName = null!;
        ShortName = null!;
        Abbreviation = null!;
        Address = null!;
        Phone = null!;
        Email = null!;
        HeadTeacherName = null!;
        Timezone = null!;
        Separator = null!;
    }

    private SchoolProfile(
        Guid id,
        string schoolName,
        string shortName,
        string abbreviation,
        string address,
        string phone,
        string email,
        string? motto,
        string headTeacherName,
        string timezone,
        int identityVersionNumber,
        int abbreviationVersionNumber,
        string separator,
        int serialWidth,
        RegNumberSerialReset serialReset,
        int regNumberVersionNumber,
        int gradingVersionNumber,
        int assessmentVersionNumber,
        int resultRulesVersionNumber,
        int ratingScalesVersionNumber,
        int developmentDomainsVersionNumber,
        int traitsVersionNumber)
        : base(id)
    {
        SchoolName = schoolName;
        ShortName = shortName;
        Abbreviation = abbreviation;
        Address = address;
        Phone = phone;
        Email = email;
        Motto = motto;
        HeadTeacherName = headTeacherName;
        Timezone = timezone;
        IdentityVersionNumber = identityVersionNumber;
        AbbreviationVersionNumber = abbreviationVersionNumber;
        Separator = separator;
        SerialWidth = serialWidth;
        SerialReset = serialReset;
        RegNumberVersionNumber = regNumberVersionNumber;
        GradingVersionNumber = gradingVersionNumber;
        AssessmentVersionNumber = assessmentVersionNumber;
        ResultRulesVersionNumber = resultRulesVersionNumber;
        RatingScalesVersionNumber = ratingScalesVersionNumber;
        DevelopmentDomainsVersionNumber = developmentDomainsVersionNumber;
        TraitsVersionNumber = traitsVersionNumber;
    }

    /// <summary>Full school name (spec 6.2.3). Appears in full on the result sheet header.</summary>
    public string SchoolName { get; private set; }

    /// <summary>Used where the full name will not fit, for example the pin slip.</summary>
    public string ShortName { get; private set; }

    /// <summary>
    /// Frozen into each registration number at the moment of issue (6.2.2). Seeded <see cref="SeededAbbreviation"/>;
    /// editable only through TASK-0005c's own endpoint, never through <see cref="UpdateIdentity"/>.
    /// </summary>
    public string Abbreviation { get; private set; }

    /// <summary>Multi-line permitted.</summary>
    public string Address { get; private set; }

    /// <summary>Nigerian format, normalised to <c>+234</c> form on save — see <see cref="NigerianPhoneNumber"/>.</summary>
    public string Phone { get; private set; }

    /// <summary>Stored lower-invariant, matching the admin-account email convention.</summary>
    public string Email { get; private set; }

    /// <summary>Printed under the school name on the result sheet if present. <see langword="null"/> when unset.</summary>
    public string? Motto { get; private set; }

    /// <summary>Printed above the head teacher's signature block.</summary>
    public string HeadTeacherName { get; private set; }

    /// <summary>Always <see cref="FixedTimezone"/> — stored so it can be echoed, never changed after seeding.</summary>
    public string Timezone { get; private set; }

    /// <summary>
    /// The identity group's optimistic-concurrency pointer. Echoed by <c>GET /settings</c> as
    /// <c>identity.versionNumber</c>; a <c>PATCH</c> must echo the same value back as
    /// <c>expectedVersion</c>. Starts at 0 (installed, never yet saved through this endpoint) and
    /// increments by exactly 1 on every successful <see cref="UpdateIdentity"/> call.
    /// </summary>
    public int IdentityVersionNumber { get; private set; }

    /// <summary>
    /// The abbreviation group's own, independent optimistic-concurrency pointer, incremented by
    /// <see cref="UpdateAbbreviation"/>.
    /// </summary>
    public int AbbreviationVersionNumber { get; private set; }

    /// <summary>6.2.4: <c>separator</c>. One of <c>/</c>, <c>-</c>, <c>.</c>.</summary>
    public string Separator { get; private set; }

    /// <summary>6.2.4: <c>serial_width</c>. 3 to 6; serials are zero-padded to this width.</summary>
    public int SerialWidth { get; private set; }

    /// <summary>6.2.4: <c>serial_reset</c>.</summary>
    public RegNumberSerialReset SerialReset { get; private set; }

    /// <summary>
    /// The reg-number group's own, independent optimistic-concurrency pointer, incremented by
    /// <see cref="UpdateRegNumber"/>.
    /// </summary>
    public int RegNumberVersionNumber { get; private set; }

    /// <summary>
    /// The grading-scale group's own, independent optimistic-concurrency pointer (TASK-0069). The
    /// scale itself lives in the separate <see cref="GradingBand"/> table — this counter has no
    /// grading fields of its own to guard, only the group's "what did the client last see" pointer,
    /// matching <see cref="AbbreviationVersionNumber"/>'s reason for existing on this singleton rather
    /// than a row that is itself replaced wholesale on every save.
    /// </summary>
    public int GradingVersionNumber { get; private set; }

    /// <summary>
    /// The assessment-structure group's own, independent optimistic-concurrency pointer (TASK-0069).
    /// See <see cref="GradingVersionNumber"/>'s remarks — same reasoning, for <see cref="AssessmentComponent"/>.
    /// </summary>
    public int AssessmentVersionNumber { get; private set; }

    /// <summary>
    /// The result-rules group's own, independent optimistic-concurrency pointer (TASK-0077). The
    /// rules themselves live in the separate <see cref="ResultRules"/> table — see
    /// <see cref="GradingVersionNumber"/>'s remarks for why the pointer lives here regardless.
    /// </summary>
    public int ResultRulesVersionNumber { get; private set; }

    /// <summary>
    /// The rating-scales group's own, independent optimistic-concurrency pointer (TASK-0072 stage 1).
    /// The scales themselves live in the separate <see cref="RatingScale"/>/<see cref="RatingScalePoint"/>
    /// tables — see <see cref="GradingVersionNumber"/>'s remarks for why the pointer lives here regardless.
    /// </summary>
    public int RatingScalesVersionNumber { get; private set; }

    /// <summary>
    /// The development-domains group's own, independent optimistic-concurrency pointer (TASK-0072
    /// stage 2b). The domains and indicators themselves live in the separate
    /// <see cref="DevelopmentDomain"/>/<see cref="DevelopmentIndicator"/> tables — see
    /// <see cref="GradingVersionNumber"/>'s remarks for why the pointer lives here regardless.
    /// </summary>
    public int DevelopmentDomainsVersionNumber { get; private set; }

    /// <summary>
    /// The traits group's own, independent optimistic-concurrency pointer (TASK-0072 stage 3b). The
    /// traits and trait blocks themselves live in the separate <see cref="Trait"/>/<see cref="TraitBlock"/>
    /// tables — see <see cref="GradingVersionNumber"/>'s remarks for why the pointer lives here regardless.
    /// </summary>
    public int TraitsVersionNumber { get; private set; }

    /// <summary>
    /// Applies a <c>PATCH /settings/identity</c> edit (spec 6.2.3's identity fields, minus
    /// <see cref="Abbreviation"/> and <see cref="Timezone"/>, both owned elsewhere) and bumps
    /// <see cref="IdentityVersionNumber"/>.
    /// </summary>
    /// <remarks>
    /// Trusts its inputs: FluentValidation has already rejected anything that would fail here, per
    /// the pipeline's "no input validation in the handler or the domain mutation" convention. The one
    /// exception is <paramref name="phone"/>'s normalisation, which is a transformation the validator
    /// deliberately leaves to this method rather than duplicating.
    /// </remarks>
    public void UpdateIdentity(
        string schoolName,
        string shortName,
        string address,
        string phone,
        string email,
        string? motto,
        string headTeacherName)
    {
        SchoolName = schoolName.Trim();
        ShortName = shortName.Trim();
        Address = address.Trim();
        Phone = NigerianPhoneNumber.TryNormalize(phone, out var normalizedPhone) ? normalizedPhone : phone.Trim();
        Email = email.Trim().ToLowerInvariant();
        Motto = string.IsNullOrWhiteSpace(motto) ? null : motto.Trim();
        HeadTeacherName = headTeacherName.Trim();
        IdentityVersionNumber++;
    }

    /// <summary>
    /// Applies a <c>PATCH /settings/abbreviation</c> edit (spec 6.2.4) and bumps
    /// <see cref="AbbreviationVersionNumber"/>. Rewrites nothing else: an already-issued registration
    /// number is frozen at the moment it was issued (spec 6.2.4, 6.5.10) and this method has no
    /// reference to any such number to rewrite even in principle — TASK-0051 owns issuance entirely.
    /// A value already used historically is accepted without complaint (spec 6.2.11): abbreviations
    /// are not unique over time.
    /// </summary>
    /// <remarks>
    /// Trusts its input, per <see cref="UpdateIdentity"/>'s own convention — FluentValidation has
    /// already checked length and the confirmation token.
    /// </remarks>
    public void UpdateAbbreviation(string abbreviation)
    {
        Abbreviation = abbreviation.Trim();
        AbbreviationVersionNumber++;
    }

    /// <summary>
    /// Applies a <c>PATCH /settings/reg-number</c> edit (spec 6.2.4) and bumps
    /// <see cref="RegNumberVersionNumber"/>. <see cref="YearSource"/> is not a parameter here — it is
    /// fixed and this method has no way to change it, matching <see cref="Timezone"/>'s own pattern.
    /// </summary>
    /// <remarks>
    /// Trusts its input: the width-reduction check against the counter (spec 6.2.10) runs BEFORE this
    /// is called, in the handler, because it needs a repository read this domain method has no access
    /// to — this method only ever sees inputs already cleared to write.
    /// </remarks>
    public void UpdateRegNumber(string separator, int serialWidth, RegNumberSerialReset serialReset)
    {
        Separator = separator;
        SerialWidth = serialWidth;
        SerialReset = serialReset;
        RegNumberVersionNumber++;
    }

    /// <summary>
    /// Bumps <see cref="GradingVersionNumber"/> for a successful <c>PUT /settings/grading</c> or
    /// <c>POST /settings/grading/reset</c> save (TASK-0069). The bands themselves are written through
    /// <c>IGradingBandRepository.ReplaceAllAsync</c>, not through this entity — this method only
    /// advances the group's optimistic-concurrency pointer, in the same transaction.
    /// </summary>
    public void IncrementGradingVersion() => GradingVersionNumber++;

    /// <summary>
    /// Bumps <see cref="AssessmentVersionNumber"/> for a successful <c>PUT /settings/assessment</c>
    /// save (TASK-0069). See <see cref="IncrementGradingVersion"/>'s remarks — same reasoning.
    /// </summary>
    public void IncrementAssessmentVersion() => AssessmentVersionNumber++;

    /// <summary>
    /// Bumps <see cref="ResultRulesVersionNumber"/> for a successful <c>PUT /settings/result-rules</c>
    /// save (TASK-0077). See <see cref="IncrementGradingVersion"/>'s remarks — same reasoning.
    /// </summary>
    public void IncrementResultRulesVersion() => ResultRulesVersionNumber++;

    /// <summary>
    /// Bumps <see cref="RatingScalesVersionNumber"/> for a successful <c>PUT /settings/rating-scales</c>
    /// save (TASK-0072 stage 1). See <see cref="IncrementGradingVersion"/>'s remarks — same reasoning.
    /// </summary>
    public void IncrementRatingScalesVersion() => RatingScalesVersionNumber++;

    /// <summary>
    /// Bumps <see cref="DevelopmentDomainsVersionNumber"/> for a successful
    /// <c>PUT /settings/development-domains</c> save (TASK-0072 stage 2b). See
    /// <see cref="IncrementGradingVersion"/>'s remarks — same reasoning.
    /// </summary>
    public void IncrementDevelopmentDomainsVersion() => DevelopmentDomainsVersionNumber++;

    /// <summary>
    /// Bumps <see cref="TraitsVersionNumber"/> for a successful <c>PUT /settings/traits</c> save
    /// (TASK-0072 stage 3b). See <see cref="IncrementGradingVersion"/>'s remarks — same reasoning.
    /// </summary>
    public void IncrementTraitsVersion() => TraitsVersionNumber++;

    /// <summary>
    /// TEST-ONLY SEAM. Builds an instance with arbitrary starting state, matching the migration
    /// seed's shape. Production code never constructs a <see cref="SchoolProfile"/> — the row already
    /// exists from the moment the migration runs — so there is no public factory to reuse; this one
    /// exists purely because a unit test cannot exercise <see cref="UpdateIdentity"/> without an
    /// instance to call it on.
    /// </summary>
    internal static SchoolProfile CreateForTesting(
        Guid id,
        string schoolName = "",
        string shortName = "",
        string abbreviation = SeededAbbreviation,
        string address = "",
        string phone = "",
        string email = "",
        string? motto = null,
        string headTeacherName = "",
        string timezone = FixedTimezone,
        int identityVersionNumber = 0,
        int abbreviationVersionNumber = 0,
        string separator = DefaultSeparator,
        int serialWidth = DefaultSerialWidth,
        RegNumberSerialReset serialReset = DefaultSerialReset,
        int regNumberVersionNumber = 0,
        int gradingVersionNumber = 0,
        int assessmentVersionNumber = 0,
        int resultRulesVersionNumber = 0,
        int ratingScalesVersionNumber = 0,
        int developmentDomainsVersionNumber = 0,
        int traitsVersionNumber = 0) =>
        new(
            id,
            schoolName,
            shortName,
            abbreviation,
            address,
            phone,
            email,
            motto,
            headTeacherName,
            timezone,
            identityVersionNumber,
            abbreviationVersionNumber,
            separator,
            serialWidth,
            serialReset,
            regNumberVersionNumber,
            gradingVersionNumber,
            assessmentVersionNumber,
            resultRulesVersionNumber,
            ratingScalesVersionNumber,
            developmentDomainsVersionNumber,
            traitsVersionNumber);
}
