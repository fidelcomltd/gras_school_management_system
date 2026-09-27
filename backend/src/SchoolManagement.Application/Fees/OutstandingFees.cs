using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Fees;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Fees;

/// <summary>Spec 6.2.13's bulk grid: an arm's pupils against one outstanding-fee figure for a term. Blank is the normal case.</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="ArmName">For example Primary 3A.</param>
/// <param name="TermId">The term.</param>
/// <param name="TermName">For example First Term.</param>
/// <param name="Locked">True once the arm's results for the term are published (human ruling 2026-09-27).</param>
/// <param name="Rows">The arm's active roster, surname first.</param>
public sealed record OutstandingFeeSheetDto(Guid ArmId, string ArmName, Guid TermId, string TermName, bool Locked, IReadOnlyList<OutstandingFeeRowDto> Rows);

/// <summary>One pupil's figure.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="DisplayName">Surname first.</param>
/// <param name="RegistrationNumber">As issued.</param>
/// <param name="Amount">Naira; null when blank, which prints as a dash.</param>
public sealed record OutstandingFeeRowDto(Guid PupilId, string DisplayName, string? RegistrationNumber, int? Amount);

/// <summary><c>GET /api/v1/arms/{armId}/outstanding-fees?termId=</c>. Needs <c>fee.manage</c>.</summary>
/// <param name="ArmId">From the route.</param>
/// <param name="TermId">A term of the arm's session.</param>
public sealed record GetOutstandingFeesQuery(Guid ArmId, Guid TermId) : IQuery<Result<OutstandingFeeSheetDto>>;

/// <summary>The term is required.</summary>
internal sealed class GetOutstandingFeesQueryValidator : AbstractValidator<GetOutstandingFeesQuery>
{
    /// <summary>Configures the rules.</summary>
    public GetOutstandingFeesQueryValidator() => RuleFor(query => query.TermId).NotEmpty();
}

/// <summary>Handles <see cref="GetOutstandingFeesQuery"/>.</summary>
internal sealed class GetOutstandingFeesHandler(OutstandingFeeReader reader, IResultSetRepository resultSets, IFeeNoticeRepository fees)
    : IRequestHandler<GetOutstandingFeesQuery, Result<OutstandingFeeSheetDto>>
{
    /// <inheritdoc />
    public async Task<Result<OutstandingFeeSheetDto>> HandleAsync(GetOutstandingFeesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await reader.LoadAsync(request.ArmId, request.TermId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<OutstandingFeeSheetDto>(loaded.Error);
        }

        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(request.ArmId, request.TermId, cancellationToken).ConfigureAwait(false);
        var figures = resultSet is null ? [] : await fees.ListOutstandingReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(loaded.Value.ToDto(resultSet?.State == ResultSetState.Published, figures));
    }
}

/// <summary>One pupil's figure as submitted.</summary>
/// <param name="PupilId">A pupil on the arm's roster.</param>
/// <param name="Amount">Naira; null clears it.</param>
public sealed record OutstandingFeeInput(Guid PupilId, int? Amount);

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/outstanding-fees</c>: saves the listed pupils' figures; pupils not listed are left as they are.
/// Refused once the arm's results for the term are published. Needs <c>fee.manage</c>.
/// </summary>
/// <param name="ArmId">From the route.</param>
/// <param name="TermId">A term of the arm's session.</param>
/// <param name="Rows">The figures.</param>
public sealed record SaveOutstandingFeesCommand(Guid ArmId, Guid TermId, IReadOnlyList<OutstandingFeeInput> Rows) : ICommand<Result<OutstandingFeeSheetDto>>;

/// <summary>Shape rules.</summary>
internal sealed class SaveOutstandingFeesCommandValidator : AbstractValidator<SaveOutstandingFeesCommand>
{
    /// <summary>Configures the rules.</summary>
    public SaveOutstandingFeesCommandValidator()
    {
        RuleFor(command => command.TermId).NotEmpty();
        RuleFor(command => command.Rows)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(rows => rows.All(row => row is not null)).WithMessage("Every row must be filled in.")
            .Must(rows => rows.Select(row => row.PupilId).Distinct().Count() == rows.Count).WithMessage("A pupil appears twice.");
        RuleForEach(command => command.Rows)
            .ChildRules(row => row.RuleFor(input => input.Amount).InclusiveBetween(0, FeeAmount.MaxAmount).When(input => input.Amount is not null)
                .WithMessage($"An outstanding figure must be between 0 and {FeeAmount.MaxAmount:N0} naira."))
            .When(command => command.Rows is not null && command.Rows.All(row => row is not null));
    }
}

/// <summary>Handles <see cref="SaveOutstandingFeesCommand"/>.</summary>
internal sealed class SaveOutstandingFeesHandler(
    OutstandingFeeReader reader,
    IResultSetRepository resultSets,
    IFeeNoticeRepository fees,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveOutstandingFeesCommand, Result<OutstandingFeeSheetDto>>
{
    /// <summary>Stable code for the published lock.</summary>
    public const string LockedErrorCode = "outstanding_fee.result_set_published";

    /// <inheritdoc />
    public async Task<Result<OutstandingFeeSheetDto>> HandleAsync(SaveOutstandingFeesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await reader.LoadAsync(request.ArmId, request.TermId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<OutstandingFeeSheetDto>(loaded.Error);
        }

        var sheet = loaded.Value;
        var roster = sheet.Roster.Select(pupil => pupil.PupilId).ToHashSet();
        if (request.Rows.Any(row => !roster.Contains(row.PupilId)))
        {
            return Result.Failure<OutstandingFeeSheetDto>(Error.Validation(
                "outstanding_fee.pupil_not_in_arm", $"Every pupil must be on {sheet.ArmName}'s roster. Reload the page and try again."));
        }

        // Row-locked before the state check, as attendance: a publish either lands first (and this is refused) or waits.
        var resultSet = await resultSets.FindTrackedByArmTermForUpdateAsync(request.ArmId, request.TermId, cancellationToken).ConfigureAwait(false);
        if (resultSet is { State: ResultSetState.Published })
        {
            return Result.Failure<OutstandingFeeSheetDto>(Error.Conflict(
                LockedErrorCode,
                $"{sheet.ArmName}'s {sheet.Term.Name} results are published, so the outstanding figures are locked. Withdraw the results to correct one."));
        }

        var existing = resultSet is null ? [] : await fees.ListOutstandingTrackedAsync(resultSet.Id, cancellationToken).ConfigureAwait(false);
        var byPupil = existing.ToDictionary(fee => fee.PupilId);
        var changed = 0;
        foreach (var row in request.Rows)
        {
            var current = byPupil.GetValueOrDefault(row.PupilId);
            if (row.Amount is not { } amount)
            {
                if (current is not null)
                {
                    await fees.RemoveOutstandingAsync(current, cancellationToken).ConfigureAwait(false);
                    byPupil.Remove(row.PupilId);
                    changed++;
                }

                continue;
            }

            if (current is not null)
            {
                if (current.Amount != amount)
                {
                    current.Change(amount);
                    changed++;
                }

                continue;
            }

            if (resultSet is null)
            {
                var created = ResultSet.Create(Guid.CreateVersion7(), request.ArmId, request.TermId);
                if (created.IsFailure)
                {
                    return Result.Failure<OutstandingFeeSheetDto>(created.Error);
                }

                resultSet = created.Value;
                await resultSets.AddAsync(resultSet, cancellationToken).ConfigureAwait(false);
            }

            var added = OutstandingFee.Create(Guid.CreateVersion7(), resultSet.Id, row.PupilId, amount);
            if (added.IsFailure)
            {
                return Result.Failure<OutstandingFeeSheetDto>(added.Error);
            }

            await fees.AddOutstandingAsync(added.Value, cancellationToken).ConfigureAwait(false);
            byPupil[row.PupilId] = added.Value;
            changed++;
        }

        // Counts only: what one family owes is not audit metadata.
        if (changed > 0 && resultSet is not null)
        {
            await auditSink.RecordAsync(
                Privileges.Fee.Manage,
                "result_set",
                resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
                metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["armId"] = request.ArmId.ToString("D", CultureInfo.InvariantCulture),
                    ["termId"] = request.TermId.ToString("D", CultureInfo.InvariantCulture),
                    ["outstandingFiguresChanged"] = changed,
                },
                actorAdminId: currentUser.UserId,
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(sheet.ToDto(false, [.. byPupil.Values]));
    }
}

/// <summary>The arm, term and roster an outstanding-fee sheet is built from.</summary>
internal sealed record OutstandingFeeContext(Guid ArmId, string ArmName, Domain.Sessions.Term Term, IReadOnlyList<ArmRosterPupil> Roster)
{
    /// <summary>The sheet with these figures.</summary>
    public OutstandingFeeSheetDto ToDto(bool locked, IReadOnlyList<OutstandingFee> figures)
    {
        var byPupil = figures.ToDictionary(fee => fee.PupilId, fee => fee.Amount);
        return new OutstandingFeeSheetDto(
            ArmId,
            ArmName,
            Term.Id,
            Term.Name,
            locked,
            [.. Roster
                .Select(pupil => new OutstandingFeeRowDto(
                    pupil.PupilId,
                    Weekly.WeeklyProjection.DisplayName(pupil.Surname, pupil.FirstName, pupil.MiddleName),
                    pupil.RegistrationNumber,
                    byPupil.TryGetValue(pupil.PupilId, out var amount) ? amount : null))
                .OrderBy(row => row.DisplayName, StringComparer.Ordinal)]);
    }
}

/// <summary>Loads an <see cref="OutstandingFeeContext"/>.</summary>
internal sealed class OutstandingFeeReader(IArmRepository arms, IClassLevelRepository classLevels, ITermRepository terms, IEnrolmentRepository enrolments)
{
    /// <summary>The arm and term, checked to belong together, with the arm's active roster.</summary>
    public async Task<Result<OutstandingFeeContext>> LoadAsync(Guid armId, Guid termId, CancellationToken cancellationToken)
    {
        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<OutstandingFeeContext>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<OutstandingFeeContext>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        if (term.SessionId != arm.SessionId)
        {
            return Result.Failure<OutstandingFeeContext>(Error.Validation("outstanding_fee.term_session_mismatch", "The term must belong to the arm's session."));
        }

        var level = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.Id == arm.ClassLevelId);
        var roster = await enrolments.ListActiveRosterByArmAsync(arm.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OutstandingFeeContext(arm.Id, ArmDisplayName.Compose(level?.Name ?? string.Empty, arm.Label), term, roster));
    }
}
