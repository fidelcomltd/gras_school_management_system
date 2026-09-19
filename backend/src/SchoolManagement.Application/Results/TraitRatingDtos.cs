using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// One trait block on the entry grid (TASK-0083 stage 1; the human's amendment to stage 0's proposal,
/// 2026-09-19: one shape, not two). <see cref="Scale"/> and <see cref="Traits"/> reuse the same DTOs
/// <c>SettingsTraitsGroupDto</c> already exposes — this is the same data, read for entry rather than
/// for editing the settings screen.
/// </summary>
/// <param name="Domain">Affective or psychomotor.</param>
/// <param name="Scale">The scale this block's traits are rated against, with its points and legend.</param>
/// <param name="Traits">Every ACTIVE trait in this block, in display order. An archived trait leaves this list; its existing ratings are kept, just not shown here.</param>
public sealed record TraitRatingBlockDto(TraitDomain Domain, RatingScaleDto Scale, IReadOnlyList<TraitDto> Traits);

/// <summary>One pupil's row on the grid (TASK-0083 stage 1) — every active pupil in the arm, including one with no ratings entered at all.</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle", same convention as <see cref="ScoreSheetRowDto"/>.</param>
/// <param name="Ratings">
/// Trait id to the chosen point id, one key per ACTIVE trait — <see langword="null"/> for an unrated
/// cell. Only active traits are keys; an archived trait's existing rating is kept in storage but is
/// not a key here, because it has left the entry screen.
/// </param>
public sealed record TraitRatingRowDto(
    string PupilId, string? RegistrationNumber, string DisplayName, IReadOnlyDictionary<string, string?> Ratings);

/// <summary>
/// One arm's trait-rating grid for one term (TASK-0083 stage 1; spec §6.7.7, §6.7.12 amendment).
/// Arm-scoped rather than result-set-scoped, mirroring <see cref="ScoreSheetDto"/> — see
/// <c>TraitRatingEndpoints</c>'s own remarks for why.
/// </summary>
/// <param name="ArmId">The arm this grid belongs to.</param>
/// <param name="TermId">The term this grid is for.</param>
/// <param name="Version">
/// Opaque, derived from the ratings the grid covers (see <c>TraitRatingVersion</c>).
/// <see langword="null"/> before any rating exists. Send back unchanged on <c>PUT</c> to detect a
/// concurrent edit.
/// </param>
/// <param name="ResultSet"><see langword="null"/> until the first save creates one ("Not started").</param>
/// <param name="Blocks">The affective block then the psychomotor block, each with its own scale.</param>
/// <param name="Rows">Every active pupil in the arm, surname then id — fixed, never affected by ratings.</param>
public sealed record TraitRatingSheetDto(
    string ArmId,
    string TermId,
    string? Version,
    ResultSetSummaryDto? ResultSet,
    IReadOnlyList<TraitRatingBlockDto> Blocks,
    IReadOnlyList<TraitRatingRowDto> Rows);
