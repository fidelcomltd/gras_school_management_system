using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>GET /api/v1/arms/{armId}/attendance?termId=</c> (TASK-0086 stage A; spec §6.7.7, §6.7.12
/// amendment) — one arm's attendance sheet for one term: every active pupil as a row, including
/// one with nothing entered at all.
/// </summary>
/// <param name="ArmId">The arm to read, from the route.</param>
/// <param name="TermId">The term this sheet is for.</param>
public sealed record GetAttendanceQuery(string ArmId, string TermId) : IQuery<Result<AttendanceSheetDto>>;

/// <summary>Structural checks only.</summary>
internal sealed class GetAttendanceQueryValidator : AbstractValidator<GetAttendanceQuery>
{
    public GetAttendanceQueryValidator()
    {
        RuleFor(query => query.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
    }
}
