namespace SchoolManagement.Application.Subjects;

/// <summary>One changed (subject, level) pair in a <see cref="SaveSubjectMappingGridResponse"/> preview or result.</summary>
public sealed record SubjectMappingChangeDto(string SubjectId, string SubjectName, string ClassLevelId, string ClassLevelName);

/// <summary>
/// The shared response envelope for <c>PUT /subject-mappings</c>, <c>POST /subject-mappings/copy</c>
/// and <c>POST /subject-mappings/prefill</c> (TASK-0070 delta amendment 5: "the same
/// <c>SaveSubjectMappingGridResponse</c> envelope as the grid save and copy") — spec 6.6.5's preview,
/// "This will add 12 mappings and end 2 mappings."
/// </summary>
/// <param name="Additions">Mappings added (or, under <see cref="DryRun"/>, previewed).</param>
/// <param name="Endings">Mappings ended (or previewed). Always empty for <c>copy</c> and <c>prefill</c>.</param>
/// <param name="DryRun">Echoes the request's <c>dryRun</c> — <see langword="true"/> means nothing was written.</param>
public sealed record SaveSubjectMappingGridResponse(
    IReadOnlyList<SubjectMappingChangeDto> Additions,
    IReadOnlyList<SubjectMappingChangeDto> Endings,
    bool DryRun);
