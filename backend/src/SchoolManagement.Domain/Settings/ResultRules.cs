using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// The result-rules singleton (spec 6.2.8): the fields the computation engine obeys — tie-break,
/// position scope, level position, pass mark, promotion threshold, core-subject requirement and
/// minimum subjects for position. Also holds the annual computation method and its weights (spec
/// 6.2.8's own table groups them together, even though 6.2.10 locks them on a different trigger).
/// </summary>
/// <remarks>
/// <para>
/// A SINGLETON BY CONVENTION, exactly one row at <see cref="SingletonId"/>, created once by the
/// migration's seed data (<c>ResultRulesConfiguration.HasData</c>) and never inserted again — same
/// convention as <see cref="SchoolProfile"/>. There is no "create" endpoint, only <see cref="Update"/>.
/// </para>
/// <para>
/// TRUSTS ITS INPUTS. <see cref="Update"/> performs no validation of its own — the field-shape rules
/// (spec 6.2.8's table) run in <c>UpdateResultRulesCommandValidator</c>, and the two hard locks (spec
/// 6.2.10) are evaluated by <c>UpdateResultRulesCommandHandler</c> against live session/publication
/// state this entity cannot see — same division of labour as <see cref="SchoolProfile.UpdateIdentity"/>.
/// </para>
/// <para>
/// <see cref="CoreSubjectIds"/> is seeded EMPTY (spec 6.2.8: "Set by the administrator once subjects
/// exist") — never guessed from a subject name, because the seeded subjects are named "English
/// Language", not spec 6.2.8's own example "English Studies" (TASK-0077 dispatch note).
/// </para>
/// </remarks>
public sealed class ResultRules : Entity<Guid>
{
    /// <summary>The one row this entity ever has.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000601");

    /// <summary>6.2.8: <c>annual_method</c> default.</summary>
    public const AnnualMethod DefaultAnnualMethod = Settings.AnnualMethod.SimpleAverage;

    /// <summary>6.2.8: <c>primary_position_scope</c> default.</summary>
    public const PrimaryPositionScope DefaultPrimaryPositionScope = Settings.PrimaryPositionScope.Arm;

    /// <summary>6.2.8: <c>show_level_position</c> default.</summary>
    public const bool DefaultShowLevelPosition = true;

    /// <summary>6.2.8: <c>tie_break_rule</c> default.</summary>
    public const TieBreakRule DefaultTieBreakRule = Settings.TieBreakRule.SharedPosition;

    /// <summary>6.2.8: <c>pass_mark</c> default.</summary>
    public const int DefaultPassMark = 40;

    /// <summary>6.2.8: <c>promotion_threshold</c> default.</summary>
    public const int DefaultPromotionThreshold = 40;

    /// <summary>6.2.8: <c>require_core_pass</c> default.</summary>
    public const bool DefaultRequireCorePass = true;

    /// <summary>6.2.8: <c>min_subjects_for_position</c> default.</summary>
    public const int DefaultMinSubjectsForPosition = 1;

    /// <summary>6.2.8: 0 to 100 inclusive, for <c>pass_mark</c>, <c>promotion_threshold</c> and each weight.</summary>
    public const int MinPercent = 0;

    /// <summary>See <see cref="MinPercent"/>.</summary>
    public const int MaxPercent = 100;

    private readonly List<Guid> _coreSubjectIds = [];

    // EF Core materialisation constructor.
    private ResultRules()
        : base()
    {
    }

    private ResultRules(
        Guid id,
        AnnualMethod annualMethod,
        int? weightFirst,
        int? weightSecond,
        int? weightThird,
        PrimaryPositionScope primaryPositionScope,
        bool showLevelPosition,
        TieBreakRule tieBreakRule,
        int passMark,
        int promotionThreshold,
        bool requireCorePass,
        IEnumerable<Guid> coreSubjectIds,
        int minSubjectsForPosition)
        : base(id)
    {
        AnnualMethod = annualMethod;
        WeightFirst = weightFirst;
        WeightSecond = weightSecond;
        WeightThird = weightThird;
        PrimaryPositionScope = primaryPositionScope;
        ShowLevelPosition = showLevelPosition;
        TieBreakRule = tieBreakRule;
        PassMark = passMark;
        PromotionThreshold = promotionThreshold;
        RequireCorePass = requireCorePass;
        _coreSubjectIds.AddRange(coreSubjectIds);
        MinSubjectsForPosition = minSubjectsForPosition;
    }

    /// <summary>Simple average or weighted (spec 6.2.8). Locked once Third Term is published for any arm (6.2.10).</summary>
    public AnnualMethod AnnualMethod { get; private set; }

    /// <summary>Required when <see cref="AnnualMethod"/> is <see cref="Settings.AnnualMethod.Weighted"/>. Locked with <see cref="AnnualMethod"/>.</summary>
    public int? WeightFirst { get; private set; }

    /// <summary>See <see cref="WeightFirst"/>.</summary>
    public int? WeightSecond { get; private set; }

    /// <summary>See <see cref="WeightFirst"/>.</summary>
    public int? WeightThird { get; private set; }

    /// <summary>Arm or level (spec 6.2.8). Locked once anything is published in the session (6.2.10).</summary>
    public PrimaryPositionScope PrimaryPositionScope { get; private set; }

    /// <summary>Whether a second, level-wide position line prints alongside the arm position.</summary>
    public bool ShowLevelPosition { get; private set; }

    /// <summary>How a tie is broken (spec 6.2.8). Locked once anything is published in the session (6.2.10).</summary>
    public TieBreakRule TieBreakRule { get; private set; }

    /// <summary>0 to 100. A subject total at or above this is a pass.</summary>
    public int PassMark { get; private set; }

    /// <summary>0 to 100. Annual average at or above this proposes promotion.</summary>
    public int PromotionThreshold { get; private set; }

    /// <summary>When true, promotion also requires a pass in every core subject.</summary>
    public bool RequireCorePass { get; private set; }

    /// <summary>Required non-empty when <see cref="RequireCorePass"/> is true. Every id an existing active subject.</summary>
    public IReadOnlyList<Guid> CoreSubjectIds => _coreSubjectIds;

    /// <summary>A pupil with fewer scored subjects than this is excluded from position ranking.</summary>
    public int MinSubjectsForPosition { get; private set; }

    /// <summary>
    /// Builds the singleton row. Only the migration seed (<c>ResultRulesConfiguration.HasData</c>) and
    /// <c>ApiTestFixture</c>'s reseed after a TRUNCATE ever call this — there is no "create" endpoint.
    /// </summary>
    public static ResultRules CreateSeed(Guid id) =>
        new(
            id,
            DefaultAnnualMethod,
            weightFirst: null,
            weightSecond: null,
            weightThird: null,
            DefaultPrimaryPositionScope,
            DefaultShowLevelPosition,
            DefaultTieBreakRule,
            DefaultPassMark,
            DefaultPromotionThreshold,
            DefaultRequireCorePass,
            coreSubjectIds: [],
            DefaultMinSubjectsForPosition);

    /// <summary>
    /// Applies a <c>PUT /settings/result-rules</c> edit (spec 6.2.8). Trusts its inputs — see the type
    /// remarks: field-shape validation and the two hard locks both already ran in the caller.
    /// </summary>
    public void Update(
        AnnualMethod annualMethod,
        int? weightFirst,
        int? weightSecond,
        int? weightThird,
        PrimaryPositionScope primaryPositionScope,
        bool showLevelPosition,
        TieBreakRule tieBreakRule,
        int passMark,
        int promotionThreshold,
        bool requireCorePass,
        IReadOnlyList<Guid> coreSubjectIds,
        int minSubjectsForPosition)
    {
        ArgumentNullException.ThrowIfNull(coreSubjectIds);

        AnnualMethod = annualMethod;
        WeightFirst = weightFirst;
        WeightSecond = weightSecond;
        WeightThird = weightThird;
        PrimaryPositionScope = primaryPositionScope;
        ShowLevelPosition = showLevelPosition;
        TieBreakRule = tieBreakRule;
        PassMark = passMark;
        PromotionThreshold = promotionThreshold;
        RequireCorePass = requireCorePass;
        _coreSubjectIds.Clear();
        _coreSubjectIds.AddRange(coreSubjectIds);
        MinSubjectsForPosition = minSubjectsForPosition;
    }
}
