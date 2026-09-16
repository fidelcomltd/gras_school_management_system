using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>
/// Spec 6.2.9's conditional reason gate, shared by <c>UpdateGradingCommandHandler</c>,
/// <c>ResetGradingCommandHandler</c> and <c>UpdateAssessmentCommandHandler</c> — the only three saves
/// this card adds that can affect a published result: "If any [result set] is Published... requires a
/// reason of at least ten characters." No active session, or an active session with nothing
/// Published, means no reason is required at all — this is not merely "reason optional", the field is
/// ignored either way, matching <see cref="ConfigVersion.Reason"/>'s own contract.
/// </summary>
internal static class PublishedResultsReasonGate
{
    /// <summary>Stable error code for a missing reason when the gate requires one.</summary>
    public const string ReasonRequiredErrorCode = "settings.reason_required";

    /// <summary>
    /// Returns the reason to STORE on the <see cref="ConfigVersion"/> row: the trimmed
    /// <paramref name="suppliedReason"/> when a result set is Published in the active session, or
    /// <see langword="null"/> otherwise. Fails when one is required but
    /// <paramref name="suppliedReason"/> is missing or blank — length is already checked by the
    /// calling command's own validator when non-null.
    /// </summary>
    public static async Task<Result<string?>> RequireReasonIfPublishedAsync(
        string? suppliedReason,
        IAcademicSessionRepository academicSessionRepository,
        IPublishedResultsGate publishedResultsGate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(academicSessionRepository);
        ArgumentNullException.ThrowIfNull(publishedResultsGate);

        var activeSession = await academicSessionRepository.FindActiveAsync(cancellationToken).ConfigureAwait(false);
        if (activeSession is null)
        {
            return Result.Success<string?>(null);
        }

        var publishedCount = await publishedResultsGate
            .CountPublishedInSessionAsync(activeSession.Id, cancellationToken)
            .ConfigureAwait(false);

        if (publishedCount == 0)
        {
            return Result.Success<string?>(null);
        }

        var trimmed = suppliedReason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure<string?>(Error.Validation(
                ReasonRequiredErrorCode,
                "Published results exist for the active session. Give a reason for this change."));
        }

        return Result.Success<string?>(trimmed);
    }
}
