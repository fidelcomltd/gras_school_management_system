using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// The result-rules wire shape (spec 6.2.8) — both <c>GET /api/v1/settings/result-rules</c>'s
/// success body and <c>PUT /api/v1/settings/result-rules</c>'s success body.
/// </summary>
/// <param name="AnnualMethod">Simple average or weighted.</param>
/// <param name="WeightFirst">Required when <paramref name="AnnualMethod"/> is <see cref="AnnualMethod.Weighted"/>. 0 to 100.</param>
/// <param name="WeightSecond">See <paramref name="WeightFirst"/>.</param>
/// <param name="WeightThird">See <paramref name="WeightFirst"/>.</param>
/// <param name="PrimaryPositionScope">Which position prints as Position in Class: arm or level.</param>
/// <param name="ShowLevelPosition">Whether a second, level-wide position line prints alongside the arm position.</param>
/// <param name="TieBreakRule">How a tied position is broken.</param>
/// <param name="PassMark">0 to 100. A subject total at or above this is a pass.</param>
/// <param name="PromotionThreshold">0 to 100. Annual average at or above this proposes promotion.</param>
/// <param name="RequireCorePass">When true, promotion also requires a pass in every core subject.</param>
/// <param name="CoreSubjectIds">Required non-empty when <paramref name="RequireCorePass"/> is true. Every id an existing active subject.</param>
/// <param name="MinSubjectsForPosition">A pupil with fewer scored subjects than this is excluded from position ranking.</param>
/// <param name="VersionNumber">
/// The result-rules group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next save.
/// </param>
public sealed record ResultRulesDto(
    AnnualMethod AnnualMethod,
    int? WeightFirst,
    int? WeightSecond,
    int? WeightThird,
    PrimaryPositionScope PrimaryPositionScope,
    bool ShowLevelPosition,
    TieBreakRule TieBreakRule,
    int PassMark,
    int PromotionThreshold,
    bool RequireCorePass,
    IReadOnlyList<Guid> CoreSubjectIds,
    int MinSubjectsForPosition,
    int VersionNumber);
