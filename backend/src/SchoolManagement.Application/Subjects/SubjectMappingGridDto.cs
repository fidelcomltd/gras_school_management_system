namespace SchoolManagement.Application.Subjects;

/// <summary>One ticked (or untickable) cell of the grid, for one level under one subject row.</summary>
/// <param name="ClassLevelId">The column.</param>
/// <param name="Mapped">Whether an active mapping exists for this (subject, level) pair in the requested term.</param>
/// <param name="DisplayOrder"><see langword="null"/> when <see cref="Mapped"/> is <see langword="false"/>.</param>
public sealed record SubjectMappingGridCellDto(string ClassLevelId, bool Mapped, int? DisplayOrder);

/// <summary>One row of the grid — a subject and its tick against every active level.</summary>
public sealed record SubjectMappingGridSubjectRowDto(
    string SubjectId,
    string SubjectName,
    string? SubjectCode,
    IReadOnlyList<SubjectMappingGridCellDto> Cells);

/// <summary>One column header of the grid.</summary>
public sealed record SubjectMappingGridLevelDto(string ClassLevelId, string ClassLevelName, int ProgressionOrder);

/// <summary>
/// Spec 6.6.5's "warning panel" row — one arm whose subject set diverges from its level, with the
/// exception counts driving it.
/// </summary>
public sealed record SubjectMappingGridArmExceptionSummaryDto(
    string ArmId, string ArmDisplayName, int IncludeCount, int ExcludeCount);

/// <summary>
/// <c>GET /subject-mappings?term_id=</c>'s response (spec 6.6.5, 6.6.9): "the whole grid for a term:
/// levels, subjects, ticks, and the arm exception summary."
/// </summary>
public sealed record SubjectMappingGridDto(
    IReadOnlyList<SubjectMappingGridLevelDto> Levels,
    IReadOnlyList<SubjectMappingGridSubjectRowDto> Subjects,
    IReadOnlyList<SubjectMappingGridArmExceptionSummaryDto> ArmExceptions);
