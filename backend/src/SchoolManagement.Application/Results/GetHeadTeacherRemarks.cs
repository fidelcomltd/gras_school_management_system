using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/head-teacher-remarks?termId=</c> (TASK-0086 stage A; spec §6.7.7) —
/// one arm's head-teacher remark sheet for one term: every active pupil as a row, including one
/// with nothing written at all.
/// </summary>
/// <param name="ArmId">The arm to read, from the route.</param>
/// <param name="TermId">The term this sheet is for.</param>
public sealed record GetHeadTeacherRemarksQuery(string ArmId, string TermId) : IQuery<Result<RemarkSheetDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetHeadTeacherRemarksQueryValidator : AbstractValidator<GetHeadTeacherRemarksQuery>
{
    public GetHeadTeacherRemarksQueryValidator()
    {
        RuleFor(query => query.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
