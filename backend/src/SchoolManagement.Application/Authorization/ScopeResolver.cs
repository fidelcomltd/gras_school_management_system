using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Pupils;

namespace SchoolManagement.Application.Authorization;

/// <summary>
/// Implements <see cref="IScopeResolver"/> per spec 4.2.1's four resolution rules.
/// </summary>
internal sealed class ScopeResolver(
    IPupilArmOfRecordLookup pupilArmLookup,
    IResultSetArmLookup resultSetArmLookup,
    IPupilRepository pupils)
    : IScopeResolver
{
    /// <inheritdoc />
    public async Task<ScopeResolution> ResolveAsync(
        ScopeParameterKind kind,
        Guid? parameterValue,
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case ScopeParameterKind.None:
                // The privilege is not scopable; there is nothing to resolve.
                return new ScopeResolution.NotApplicable();

            case ScopeParameterKind.Arm:
                // "A request naming an arm resolves to that arm." The parameter IS the arm id.
                return parameterValue is { } armId
                    ? new ScopeResolution.ResolvedArm(armId)
                    : new ScopeResolution.Unresolvable();

            case ScopeParameterKind.Pupil:
                if (parameterValue is not { } pupilId)
                {
                    return new ScopeResolution.Unresolvable();
                }

                // Never trust a client-supplied arm id for a pupil-scoped route (spec 9.2) — the
                // lookup resolves it from the pupil's open enrolment.
                var pupilArm = await pupilArmLookup
                    .GetArmIdAsync(pupilId, cancellationToken)
                    .ConfigureAwait(false);

                if (pupilArm is { } resolvedPupilArm)
                {
                    return new ScopeResolution.ResolvedArm(resolvedPupilArm);
                }

                // A pupil with no open enrolment (a pending admission, a leaver) has no arm for an
                // arm-scoped grant to match, but is still a real target: only a school-wide grant
                // covers them (human ruling 2026-09-23). An id naming no pupil stays unresolvable.
                return await pupils.ExistsAsync(pupilId, cancellationToken).ConfigureAwait(false)
                    ? new ScopeResolution.RequiresSchoolWide()
                    : new ScopeResolution.Unresolvable();

            case ScopeParameterKind.ResultSet:
                if (parameterValue is not { } resultSetId)
                {
                    return new ScopeResolution.Unresolvable();
                }

                var resultSetArm = await resultSetArmLookup
                    .GetArmIdAsync(resultSetId, cancellationToken)
                    .ConfigureAwait(false);

                return resultSetArm is { } resolvedResultSetArm
                    ? new ScopeResolution.ResolvedArm(resolvedResultSetArm)
                    : new ScopeResolution.Unresolvable();

            case ScopeParameterKind.Level:
                // "A request naming a level, with no arm, requires the privilege school-wide. An
                // arm-scoped holder cannot perform level-wide operations even over a level
                // containing only their own arm." The level's identity plays no further part in
                // the check, so it is deliberately not consulted here.
                return new ScopeResolution.RequiresSchoolWide();

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown scope parameter kind.");
        }
    }
}
