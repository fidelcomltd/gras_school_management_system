using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Domain.Enrolments;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="IEnrolmentRepository"/>.</summary>
internal sealed class EnrolmentRepository(ApplicationDbContext context) : IEnrolmentRepository
{
    /// <inheritdoc />
    public Task AddAsync(Enrolment enrolment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enrolment);
        cancellationToken.ThrowIfCancellationRequested();

        context.Enrolments.Add(enrolment);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Enrolment?> FindOpenTrackedByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken) =>
        context.Enrolments.FirstOrDefaultAsync(
            enrolment => enrolment.PupilId == pupilId && enrolment.EffectiveTo == null, cancellationToken);

    /// <inheritdoc />
    public Task<Enrolment?> FindOpenReadOnlyByPupilIdAsync(Guid pupilId, CancellationToken cancellationToken) =>
        context.Enrolments.AsNoTracking().FirstOrDefaultAsync(
            enrolment => enrolment.PupilId == pupilId && enrolment.EffectiveTo == null, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Enrolment>> ListByPupilReadOnlyAsync(Guid pupilId, CancellationToken cancellationToken) =>
        await context.Enrolments.AsNoTracking()
            .Where(enrolment => enrolment.PupilId == pupilId)
            .OrderBy(enrolment => enrolment.EffectiveFrom)
            .ThenBy(enrolment => enrolment.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<int> CountOpenExcludingPendingByArmAsync(Guid armId, CancellationToken cancellationToken) =>
        (from enrolment in context.Enrolments.AsNoTracking()
         where enrolment.ArmId == armId && enrolment.EffectiveTo == null
         // context.Pupils already carries the pending-exclusion model-level query filter
         // (PupilConfiguration.HasQueryFilter) — joining through it, rather than re-testing
         // pupil.Status here, is the same "enforced once" choice PupilRepository itself makes, and
         // it is defence in depth: a pending pupil can never have an open enrolment by construction
         // (admission approval sets Active and opens the enrolment together), so this join should
         // never actually exclude a row in practice.
         join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
         select enrolment.Id)
        .CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArmRosterPupil>> ListActiveRosterByArmAsync(Guid armId, CancellationToken cancellationToken) =>
        await (from enrolment in context.Enrolments.AsNoTracking()
               where enrolment.ArmId == armId && enrolment.EffectiveTo == null
               join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
               // Defence in depth, same reasoning as CountOpenExcludingPendingByArmAsync above:
               // pending is already excluded by PupilConfiguration's query filter, but
               // Transferred/Withdrawn/Graduated pupils are not, and none of them belongs on a live
               // score sheet (spec 6.5.14).
               where pupil.Status == PupilStatus.Active
               orderby pupil.Surname.ToLower(), pupil.Id
               select new ArmRosterPupil(pupil.Id, pupil.RegistrationNumber, pupil.Surname, pupil.FirstName, pupil.MiddleName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArmLeaverPupil>> ListLeftDuringTermByArmAsync(
        Guid armId, DateOnly termStart, DateOnly termEnd, CancellationToken cancellationToken)
    {
        var leavers = await (from enrolment in context.Enrolments.AsNoTracking()
                             where enrolment.ArmId == armId && enrolment.EffectiveTo != null
                                   && enrolment.EffectiveTo >= termStart && enrolment.EffectiveTo <= termEnd
                                   // A pupil reinstated into this same arm is back on the roster, not a leaver.
                                   && !context.Enrolments.Any(open =>
                                       open.PupilId == enrolment.PupilId && open.ArmId == armId && open.EffectiveTo == null)
                             join pupil in context.Pupils.AsNoTracking() on enrolment.PupilId equals pupil.Id
                             orderby pupil.Surname.ToLower(), pupil.Id
                             select new ArmLeaverPupil(
                                 pupil.Id, pupil.RegistrationNumber, pupil.Surname, pupil.FirstName, pupil.MiddleName, enrolment.EffectiveTo!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A pupil who left, came back and left again in one term has two closed rows; list them once, on the latest.
        return leavers
            .GroupBy(leaver => leaver.PupilId)
            .Select(group => group.MaxBy(leaver => leaver.LeftOn)!)
            .ToList();
    }
}
