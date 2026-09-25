using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Pupils.Records;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Movement;

/// <summary>
/// <c>GET /api/v1/pupils/{id}/enrolments</c> (spec 6.5.15's enrolment history, 02 §5.2): the pupil's current class, every
/// class they have sat in, and every status change. <c>pupil.view</c>, checked in the handler like every pupil sub-record.
/// </summary>
/// <param name="Id">The pupil. From the route.</param>
public sealed record GetPupilEnrolmentsQuery(Guid Id) : IQuery<Result<PupilEnrolmentHistoryDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPupilEnrolmentsQueryValidator : AbstractValidator<GetPupilEnrolmentsQuery>;

/// <summary>Handles <see cref="GetPupilEnrolmentsQuery"/>.</summary>
internal sealed class GetPupilEnrolmentsHandler(
    PupilRecordAccess access,
    IEnrolmentRepository enrolments,
    IPupilStatusChangeRepository statusChanges,
    IArmRepository arms,
    IAcademicSessionRepository sessions,
    PupilMovementEngine engine)
    : IRequestHandler<GetPupilEnrolmentsQuery, Result<PupilEnrolmentHistoryDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilEnrolmentHistoryDto>> HandleAsync(GetPupilEnrolmentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allowed = await access.CheckAsync(request.Id, Privileges.Pupil.View, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilEnrolmentHistoryDto>(allowed.Error);
        }

        var pupil = allowed.Value;
        var history = await enrolments.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var changes = await statusChanges.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false);

        // A pupil's history spans a handful of arms, so each is loaded by id rather than listing every arm ever made.
        var armIds = history.Select(enrolment => enrolment.ArmId)
            .Concat(changes.Where(change => change.ArmId is not null).Select(change => change.ArmId!.Value))
            .Distinct();
        var armList = new List<Domain.Classes.Arm>();
        foreach (var armId in armIds)
        {
            if (await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is { } arm)
            {
                armList.Add(arm);
            }
        }

        var armNames = await engine.DisplayNamesAsync(armList, cancellationToken).ConfigureAwait(false);
        var sessionNames = new Dictionary<Guid, string>();
        foreach (var sessionId in armList.Select(arm => arm.SessionId).Distinct())
        {
            if (await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false) is { } session)
            {
                sessionNames[sessionId] = session.Name;
            }
        }

        var armSessions = armList.ToDictionary(arm => arm.Id, arm => arm.SessionId);
        var current = history.FirstOrDefault(enrolment => enrolment.IsOpen);

        return Result.Success(new PupilEnrolmentHistoryDto(
            Id(pupil.Id),
            pupil.Status,
            current is null ? null : Id(current.ArmId),
            current is null ? null : armNames.GetValueOrDefault(current.ArmId),
            history.Select(enrolment =>
            {
                var sessionId = armSessions.GetValueOrDefault(enrolment.ArmId);
                return new PupilEnrolmentDto(
                    Id(enrolment.ArmId),
                    armNames.GetValueOrDefault(enrolment.ArmId, string.Empty),
                    Id(sessionId),
                    sessionNames.GetValueOrDefault(sessionId, string.Empty),
                    enrolment.EffectiveFrom,
                    enrolment.EffectiveTo);
            }).ToList(),
            changes.Select(change => new PupilStatusChangeDto(
                change.FromStatus,
                change.ToStatus,
                change.EffectiveDate,
                change.Reason,
                change.ArmId is { } armId ? armNames.GetValueOrDefault(armId) : null,
                change.ChangedAtUtc)).ToList()));
    }

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);
}
