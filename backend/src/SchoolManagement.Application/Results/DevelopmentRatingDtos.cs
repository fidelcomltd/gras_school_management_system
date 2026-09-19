using SchoolManagement.Application.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// One rated cell (TASK-0083 stage 2) — used BOTH ways: as a GET row's value (never null; an unrated
/// indicator carries <c>{ pointId: null, comment: null }</c>) and, wrapped nullable, as a submitted
/// PUT cell, where the whole value being JSON <c>null</c>, or an object with a null
/// <see cref="PointId"/>, both mean "clear this cell" (Q1-A / Q3-A: clearing the point clears the
/// comment) — see <c>SaveDevelopmentRatingsHandler</c>'s remarks for the exact rule.
/// </summary>
/// <param name="PointId">The chosen point, or <see langword="null"/> when unrated.</param>
/// <param name="Comment">Free text up to 120 characters (Appendix E.3), or <see langword="null"/>. Never non-null without a point.</param>
public sealed record DevelopmentRatingCellDto(string? PointId, string? Comment);

/// <summary>
/// One development domain on the entry grid (TASK-0083 stage 2). Reuses <see cref="RatingScaleDto"/>
/// and <see cref="DevelopmentIndicatorDto"/> exactly as the settings screen's own DTOs shape them —
/// same convention <c>TraitRatingBlockDto</c> established for the primary grid: this is the same
/// data, read for entry rather than for editing the settings screen.
/// </summary>
/// <param name="Id">The domain's id.</param>
/// <param name="Name">The block heading, for example "Personal &amp; Physical Development".</param>
/// <param name="DisplayOrder">Printed block order within the section (Appendix E.3: "Four blocks, in this order").</param>
/// <param name="RatingScaleId">The scale this domain's indicators are rated against.</param>
/// <param name="Scale">That scale, with its points and legend.</param>
/// <param name="AllowsIndicatorComment">Whether the entry screen prints a per-indicator Comments column for this domain.</param>
/// <param name="ActiveIndicatorCount">Active indicators in this domain — what the client's own &gt;60 warning sums across every domain.</param>
/// <param name="Indicators">Every ACTIVE indicator on this domain, in display order. An archived indicator leaves this list; its existing ratings are kept.</param>
public sealed record DevelopmentRatingDomainDto(
    string Id,
    string Name,
    int DisplayOrder,
    string RatingScaleId,
    RatingScaleDto Scale,
    bool AllowsIndicatorComment,
    int ActiveIndicatorCount,
    IReadOnlyList<DevelopmentIndicatorDto> Indicators);

/// <summary>One pupil's row on the grid (TASK-0083 stage 2) — every active pupil in the arm, including one with no ratings entered at all.</summary>
/// <param name="PupilId">The pupil's id.</param>
/// <param name="RegistrationNumber"><see langword="null"/> only if somehow unissued.</param>
/// <param name="DisplayName">Composed "Surname First Middle", same convention as <see cref="ScoreSheetRowDto"/>.</param>
/// <param name="Ratings">
/// Indicator id to its cell, one key per ACTIVE indicator across every ACTIVE domain of the arm's
/// section. An unrated cell is present, not omitted — <c>{ pointId: null, comment: null }</c>. Only
/// active indicators are keys; an archived indicator's existing rating is kept in storage but is not
/// a key here, because it has left the entry screen.
/// </param>
public sealed record DevelopmentRatingRowDto(
    string PupilId, string? RegistrationNumber, string DisplayName, IReadOnlyDictionary<string, DevelopmentRatingCellDto> Ratings);

/// <summary>
/// One arm's development-rating grid for one term (TASK-0083 stage 2; spec §6.7.7, §6.7.12 amendment,
/// Appendix E.3). Arm-scoped rather than result-set-scoped, mirroring <see cref="TraitRatingSheetDto"/>
/// — see <c>DevelopmentRatingEndpoints</c>'s own remarks for why.
/// </summary>
/// <param name="ArmId">The arm this grid belongs to.</param>
/// <param name="TermId">The term this grid is for.</param>
/// <param name="Version">
/// Opaque, derived from the ratings the grid covers (see <c>DevelopmentRatingVersion</c>).
/// <see langword="null"/> before any rating exists. Send back unchanged on <c>PUT</c> to detect a
/// concurrent edit.
/// </param>
/// <param name="ResultSet"><see langword="null"/> until the first save creates one ("Not started").</param>
/// <param name="Domains">Every ACTIVE domain of the arm's section, in display order.</param>
/// <param name="ActiveIndicatorTotal">The sum of every domain's <c>activeIndicatorCount</c> — the client's &gt;60 warning reads this directly rather than summing itself.</param>
/// <param name="Rows">Every active pupil in the arm, surname then id — fixed, never affected by ratings.</param>
public sealed record DevelopmentRatingSheetDto(
    string ArmId,
    string TermId,
    string? Version,
    ResultSetSummaryDto? ResultSet,
    IReadOnlyList<DevelopmentRatingDomainDto> Domains,
    int ActiveIndicatorTotal,
    IReadOnlyList<DevelopmentRatingRowDto> Rows);
