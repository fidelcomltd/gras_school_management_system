using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>
/// Handles <see cref="CorrectRegistrationNumberCommand"/> (spec 6.5.10, "Immutability and
/// correction"). The route requires <c>pupil.regnumber.correct</c> only — see
/// <c>PupilEndpoints.MapCorrectRegistrationNumber</c>; the privilege is Super Admin only by
/// construction (excluded from every seeded non-Super-Admin role), so no additional scope check
/// runs here, the same reasoning <c>ApproveAdmissionCommandHandler</c> applies for its own
/// school-wide-only privilege.
/// </summary>
/// <remarks>
/// One transaction, the same shape <c>ApproveAdmissionCommandHandler</c> uses for its own two-table
/// write (the pupil row and the counter): the uniqueness check spans <c>pupil.registration_number</c>
/// and <c>pupil_reg_number_history</c>, and the write touches the pupil row plus a new history row.
/// Unlike that handler, there is no retry loop here — the administrator chose this exact number
/// deliberately, so a collision is a 409 to fix by typing a different number, never something to
/// retry automatically.
/// </remarks>
internal sealed class CorrectRegistrationNumberHandler(
    IPupilRepository pupils,
    IPupilRegNumberHistoryRepository history,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CorrectRegistrationNumberCommand, Result<PupilDto>>
{
    private const string EntityType = "pupil";

    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(CorrectRegistrationNumberCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pupil = await pupils.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (pupil is null)
        {
            return Result.Failure<PupilDto>(Error.NotFound(
                "pupil.not_found", "No pupil was found with that id."));
        }

        var newRegistrationNumber = request.RegistrationNumber.Trim();

        // The two-table uniqueness check (spec 6.5.10) — against the live column, excluding this
        // SAME pupil's own current row (resubmitting the number this pupil already holds must not
        // spuriously collide with itself), and against every historical alias ever recorded, for any
        // pupil.
        var collidesWithLive = await pupils
            .ExistsByRegistrationNumberAsync(newRegistrationNumber, pupil.Id, cancellationToken)
            .ConfigureAwait(false);

        var collidesWithHistory = !collidesWithLive &&
            await history.ExistsAsync(newRegistrationNumber, cancellationToken).ConfigureAwait(false);

        if (collidesWithLive || collidesWithHistory)
        {
            return Result.Failure<PupilDto>(Error.Conflict(
                "pupil.registration_number_duplicate",
                $"{newRegistrationNumber} is already in use, either as a current registration number or as a historical alias. Choose a different number."));
        }

        // Captured BEFORE the overwrite below — this is the value the history row and the audit
        // event both need, and Pupil.CorrectRegistrationNumber does not hand it back (see that
        // method's own remarks).
        var oldRegistrationNumber = pupil.RegistrationNumber;

        var correction = pupil.CorrectRegistrationNumber(newRegistrationNumber);

        if (correction.IsFailure)
        {
            return Result.Failure<PupilDto>(correction.Error);
        }

        var actorId = currentUser.UserId is { } actorIdText && Guid.TryParse(actorIdText, out var parsedActorId)
            ? parsedActorId
            : (Guid?)null;
        var now = timeProvider.GetUtcNow();

        var historyRow = PupilRegNumberHistory.Create(
            Guid.CreateVersion7(), pupil.Id, oldRegistrationNumber!, request.Reason, actorId, now);

        if (historyRow.IsFailure)
        {
            return Result.Failure<PupilDto>(historyRow.Error);
        }

        await history.AddAsync(historyRow.Value, cancellationToken).ConfigureAwait(false);

        // Spec 6.1.12 names "registration number correction" among the actions a reason is
        // mandatory for — passed through to the audit row itself, not just the history table.
        await auditSink.RecordAsync(
            Privileges.Pupil.RegNumberCorrect,
            EntityType,
            pupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["oldRegistrationNumber"] = oldRegistrationNumber,
                ["newRegistrationNumber"] = newRegistrationNumber,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            reason: request.Reason).ConfigureAwait(false);

        return Result.Success(PupilMapper.ToDto(pupil, DateOnly.FromDateTime(now.UtcDateTime)));
    }
}
