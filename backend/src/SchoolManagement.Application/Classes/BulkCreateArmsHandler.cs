using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="BulkCreateArmsCommand"/>.</summary>
internal sealed class BulkCreateArmsHandler(
    IArmRepository arms,
    IClassLevelRepository levels,
    IAcademicSessionRepository sessions,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<BulkCreateArmsCommand, Result<BulkCreateArmsResponse>>
{
    private const string EntityType = "arm";

    /// <inheritdoc />
    public async Task<Result<BulkCreateArmsResponse>> HandleAsync(
        BulkCreateArmsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sessionId = Guid.Parse(request.SessionId);
        var session = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            return Result.Failure<BulkCreateArmsResponse>(Error.Validation(
                "arm.session_not_found", "No session was found with that id."));
        }

        if (session.State == SessionState.Closed)
        {
            return Result.Failure<BulkCreateArmsResponse>(Error.Conflict(
                "arm.session_closed", $"{session.Name} is closed. Arms can only be created in an upcoming or active session."));
        }

        var allLevels = await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levelNamesById = allLevels.ToDictionary(level => level.Id, level => level.Name);

        var created = new List<Arm>();

        foreach (var entry in request.Levels)
        {
            // Spec 6.4.3: "Levels set to zero arms are skipped."
            if (entry.ArmCount == 0)
            {
                continue;
            }

            var levelId = Guid.Parse(entry.LevelId);
            var level = allLevels.FirstOrDefault(candidate => candidate.Id == levelId);

            if (level is null || level.Status != LevelStatus.Active)
            {
                return Result.Failure<BulkCreateArmsResponse>(Error.Validation(
                    "arm.level_not_found", "No active class level was found with that id."));
            }

            // Spec 6.4.8: "labels continue from the highest existing label" — grown as each new label
            // is assigned so two arms requested for the same level in this run never collide.
            var labelsInUse = allArms
                .Where(arm => arm.ClassLevelId == levelId && arm.SessionId == sessionId)
                .Select(arm => arm.Label)
                .ToList();

            var capacity = entry.Capacity ?? Arm.DefaultCapacity;

            for (var index = 0; index < entry.ArmCount; index++)
            {
                var label = ArmLabelSequencer.NextUnusedLabel(labelsInUse);
                labelsInUse.Add(label);

                var creation = Arm.Create(Guid.CreateVersion7(), levelId, sessionId, label, capacity, formTeacherAdminId: null);

                if (creation.IsFailure)
                {
                    return Result.Failure<BulkCreateArmsResponse>(creation.Error);
                }

                created.Add(creation.Value);
            }
        }

        if (!request.DryRun)
        {
            foreach (var arm in created)
            {
                await arms.AddAsync(arm, cancellationToken).ConfigureAwait(false);
            }

            await auditSink.RecordAsync(
                Privileges.Arm.Create,
                EntityType,
                entityId: null,
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["sessionId"] = request.SessionId,
                    ["count"] = created.Count,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        var response = new BulkCreateArmsResponse(created.Select(arm => ArmMapper.ToDto(arm, levelNamesById)).ToArray());
        return Result.Success(response);
    }
}
