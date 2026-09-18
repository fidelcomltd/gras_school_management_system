using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>The wire shape of a <see cref="SubjectMappingException"/> (spec 6.6.4, 6.6.9).</summary>
public sealed record SubjectExceptionDto(
    string Id,
    string ArmId,
    string SubjectId,
    string SubjectName,
    string TermId,
    SubjectExceptionMode Mode,
    string Reason);
