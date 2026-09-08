using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// Handles <see cref="DeleteLevelCommand"/>.
/// </summary>
/// <remarks>
/// Spec 6.4.2's "nothing has ever referenced it" spans arms, enrolments, subject mappings and results
/// — NONE of those tables exist in this codebase yet (TASK-0039 and later Phase 2/3 cards), so that
/// part of the precondition is DEFERRED (TASK-0038; tracked in <c>backend/docs/ASSUMPTIONS.md</c> §2
/// and <c>STATE.md</c>'s known drift) rather than faked with an always-true probe — the project
/// already regrets one such bypass (<c>SuperAdminFlagEffectivePrivilegeProvider</c>).
/// <para>
/// The ONE reference this card CAN check honestly today: another level's <c>nextLevelId</c> — that
/// table exists right now, in this same migration. See
/// <see cref="IClassLevelRepository.FindReferencingNextLevelAsync"/>; the database also carries a
/// <c>RESTRICT</c> foreign key on the same column as a backstop (<c>ClassLevelConfiguration</c>),
/// mirroring how <c>TermConfiguration</c>'s partial unique index backstops its own friendly check.
/// </para>
/// </remarks>
internal sealed class DeleteLevelHandler(
    IClassLevelRepository levels,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<DeleteLevelCommand, Result>
{
    private const string EntityType = "class_level";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(DeleteLevelCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var level = await levels.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (level is null)
        {
            return Result.Failure(Error.NotFound("level.not_found", "No class level was found with that id."));
        }

        var referencingLevel = await levels
            .FindReferencingNextLevelAsync(level.Id, cancellationToken)
            .ConfigureAwait(false);

        if (referencingLevel is not null)
        {
            return Result.Failure(Error.Conflict(
                "level.referenced",
                $"{level.Name} cannot be deleted because {referencingLevel.Name} points to it as its " +
                $"next level. Point {referencingLevel.Name} elsewhere first, or deactivate " +
                $"{level.Name} instead."));
        }

        await levels.RemoveAsync(level, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Level.Delete,
            EntityType,
            level.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
