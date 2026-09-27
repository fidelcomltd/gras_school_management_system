using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Fees;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Fees;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Fees;

/// <summary>
/// Spec 6.2.13's entry grid for one section and one term: the lines as rows, the section's class levels as columns, the
/// amounts as cells. The term is the one whose result sheets print the notice (human ruling 2026-09-27).
/// </summary>
/// <param name="SectionId">The section.</param>
/// <param name="SectionName">For example Primary.</param>
/// <param name="TermId">The term whose sheets print it.</param>
/// <param name="TermName">For example First Term.</param>
/// <param name="SessionName">For example 2026/2027.</param>
/// <param name="PreviousTermId">The term before, for "copy from previous term"; null when this is the first term on record.</param>
/// <param name="PreviousTermLabel">For example "Third Term 2025/2026".</param>
/// <param name="IsDefault">True when the section has saved no lines yet and <paramref name="Lines"/> are spec 6.2.13's seed, unsaved.</param>
/// <param name="Levels">The section's active class levels, in progression order.</param>
/// <param name="Lines">In print order.</param>
/// <param name="Version">Opaque; sent back with a save so a stale screen cannot overwrite (or delete) newer lines. Null before any save.</param>
public sealed record FeeNoticeGridDto(
    Guid SectionId,
    string SectionName,
    Guid TermId,
    string TermName,
    string SessionName,
    Guid? PreviousTermId,
    string? PreviousTermLabel,
    bool IsDefault,
    IReadOnlyList<FeeGridLevelDto> Levels,
    IReadOnlyList<FeeGridLineDto> Lines,
    string? Version);

/// <summary>A column of the grid.</summary>
/// <param name="ClassLevelId">The level.</param>
/// <param name="Name">For example Primary 3.</param>
public sealed record FeeGridLevelDto(Guid ClassLevelId, string Name);

/// <summary>A row of the grid.</summary>
/// <param name="Id">The saved line; null for an unsaved default.</param>
/// <param name="Label">As printed.</param>
/// <param name="Kind">Amount, or the one per-pupil Outstanding line.</param>
/// <param name="ShowOnPortal">Outstanding line only: whether parents see the figure on the portal. Off by default.</param>
/// <param name="Amounts">One per level that has an amount set; an outstanding line has none.</param>
public sealed record FeeGridLineDto(Guid? Id, string Label, FeeLabelKind Kind, bool ShowOnPortal, IReadOnlyList<FeeGridAmountDto> Amounts);

/// <summary>A cell.</summary>
/// <param name="ClassLevelId">The column.</param>
/// <param name="Amount">Naira. Zero prints as a dash.</param>
public sealed record FeeGridAmountDto(Guid ClassLevelId, int Amount);

/// <summary><c>GET /api/v1/fee-notices?sectionId=&amp;termId=</c>. Needs <c>fee.manage</c>.</summary>
/// <param name="SectionId">The section.</param>
/// <param name="TermId">The term whose sheets print it.</param>
public sealed record GetFeeNoticeGridQuery(Guid SectionId, Guid TermId) : IQuery<Result<FeeNoticeGridDto>>;

/// <summary>Both ids are required.</summary>
internal sealed class GetFeeNoticeGridQueryValidator : AbstractValidator<GetFeeNoticeGridQuery>
{
    /// <summary>Configures the rules.</summary>
    public GetFeeNoticeGridQueryValidator()
    {
        RuleFor(query => query.SectionId).NotEmpty();
        RuleFor(query => query.TermId).NotEmpty();
    }
}

/// <summary>Handles <see cref="GetFeeNoticeGridQuery"/>.</summary>
internal sealed class GetFeeNoticeGridHandler(FeeNoticeGridReader reader) : IRequestHandler<GetFeeNoticeGridQuery, Result<FeeNoticeGridDto>>
{
    /// <inheritdoc />
    public async Task<Result<FeeNoticeGridDto>> HandleAsync(GetFeeNoticeGridQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await reader.LoadAsync(request.SectionId, request.TermId, tracked: false, cancellationToken).ConfigureAwait(false);
        return context.IsFailure ? Result.Failure<FeeNoticeGridDto>(context.Error) : Result.Success(context.Value.ToDto());
    }
}

/// <summary>One row as the grid screen submits it.</summary>
/// <param name="Id">The saved line; null for a new one (or a default being saved for the first time).</param>
/// <param name="Label">1 to 60 characters.</param>
/// <param name="Kind">Amount or Outstanding; a saved line's kind never changes.</param>
/// <param name="ShowOnPortal">Outstanding line only.</param>
/// <param name="Amounts">Cells for this term; a null amount clears the cell. Levels not listed are left as they are.</param>
public sealed record FeeGridLineInput(Guid? Id, string Label, FeeLabelKind Kind, bool ShowOnPortal, IReadOnlyList<FeeGridAmountInput> Amounts);

/// <summary>A cell as submitted.</summary>
/// <param name="ClassLevelId">The column.</param>
/// <param name="Amount">Naira; null clears the cell.</param>
public sealed record FeeGridAmountInput(Guid ClassLevelId, int? Amount);

/// <summary>
/// <c>PUT /api/v1/fee-notices</c> (spec 6.2.13): the whole grid in one save. The rows as listed become the section's lines in
/// that order; a saved line left out is removed, with its amounts in every term. Needs <c>fee.manage</c>.
/// </summary>
/// <param name="SectionId">The section.</param>
/// <param name="TermId">The term whose sheets print it.</param>
/// <param name="Lines">Every line, in print order. Empty removes the notice: the section's sheets then print no fees block.</param>
/// <param name="Version">The <see cref="FeeNoticeGridDto.Version"/> the screen was loaded with; 409 when the grid has changed since.</param>
public sealed record SaveFeeNoticeGridCommand(Guid SectionId, Guid TermId, IReadOnlyList<FeeGridLineInput> Lines, string? Version)
    : ICommand<Result<FeeNoticeGridDto>>;

/// <summary>Shape rules; the section's levels and saved lines are checked in the handler.</summary>
internal sealed class SaveFeeNoticeGridCommandValidator : AbstractValidator<SaveFeeNoticeGridCommand>
{
    /// <summary>Configures the rules.</summary>
    public SaveFeeNoticeGridCommandValidator()
    {
        RuleFor(command => command.SectionId).NotEmpty();
        RuleFor(command => command.TermId).NotEmpty();
        RuleFor(command => command.Lines)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(lines => lines.All(line => line is not null && line.Amounts is not null)).WithMessage("Every line must be filled in.")
            .Must(lines => lines.Count <= FeeLabel.MaxLabelsPerSection).WithMessage($"A fee notice can have at most {FeeLabel.MaxLabelsPerSection} lines.")
            .Must(lines => lines.Count(line => line.Kind == FeeLabelKind.Outstanding) <= 1).WithMessage("Only one line can be the outstanding-fee line.")
            .Must(lines => lines.Where(line => line.Id is not null).Select(line => line.Id).Distinct().Count() == lines.Count(line => line.Id is not null))
            .WithMessage("A line appears twice.")
            .Must(lines => NoDuplicateLabels(lines))
            .WithMessage("Two lines have the same label.");
        RuleForEach(command => command.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(input => input.Label).NotEmpty().MaximumLength(FeeLabel.LabelMaxLength);
                line.RuleFor(input => input.Kind).IsInEnum();
                line.RuleFor(input => input.Amounts)
                    .Must(amounts => amounts.Count == 0).When(input => input.Kind == FeeLabelKind.Outstanding)
                    .WithMessage("The outstanding-fee line is typed per pupil, not per class.");
                line.RuleFor(input => input.Amounts)
                    .Must(amounts => amounts.Select(amount => amount.ClassLevelId).Distinct().Count() == amounts.Count)
                    .WithMessage("A class appears twice on one line.");
                line.RuleForEach(input => input.Amounts).ChildRules(amount =>
                    amount.RuleFor(cell => cell.Amount).InclusiveBetween(0, FeeAmount.MaxAmount).When(cell => cell.Amount is not null)
                        .WithMessage($"A fee amount must be between 0 and {FeeAmount.MaxAmount:N0} naira."));
            })
            .When(command => command.Lines is not null && command.Lines.All(line => line is not null && line.Amounts is not null));
    }

    // Blank labels are the per-line rule's to report, not a duplicate.
    private static bool NoDuplicateLabels(IReadOnlyList<FeeGridLineInput> lines)
    {
        var labels = lines.Select(line => line.Label?.Trim().ToUpperInvariant()).Where(label => !string.IsNullOrEmpty(label)).ToList();
        return labels.Distinct(StringComparer.Ordinal).Count() == labels.Count;
    }
}

/// <summary>Handles <see cref="SaveFeeNoticeGridCommand"/>.</summary>
internal sealed class SaveFeeNoticeGridHandler(
    FeeNoticeGridReader reader,
    IFeeNoticeRepository fees,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<SaveFeeNoticeGridCommand, Result<FeeNoticeGridDto>>
{
    /// <inheritdoc />
    public async Task<Result<FeeNoticeGridDto>> HandleAsync(SaveFeeNoticeGridCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await reader.LoadAsync(request.SectionId, request.TermId, tracked: true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure<FeeNoticeGridDto>(loaded.Error);
        }

        var grid = loaded.Value;

        // Removing a line takes its amounts in every term, so a save from a stale screen must never run.
        if (!string.Equals(grid.Version(), request.Version, StringComparison.Ordinal))
        {
            return Result.Failure<FeeNoticeGridDto>(Error.Conflict(
                "fee.stale_version", "These fee lines were changed since you opened them. Reload the page before saving again."));
        }

        var levelIds = grid.Levels.Select(level => level.Id).ToHashSet();
        var saved = grid.Labels.ToDictionary(label => label.Id);
        foreach (var line in request.Lines)
        {
            if (line.Id is { } id && !saved.ContainsKey(id))
            {
                return Result.Failure<FeeNoticeGridDto>(Error.Conflict(
                    "fee.line_not_found", $"The line \"{line.Label}\" is no longer on this notice. Reload the page and try again."));
            }

            if (line.Id is { } existing && saved[existing].Kind != line.Kind)
            {
                return Result.Failure<FeeNoticeGridDto>(Error.Validation("fee.kind_immutable", $"The line \"{line.Label}\" cannot change between an amount and the outstanding figure."));
            }

            if (line.Amounts.Any(amount => !levelIds.Contains(amount.ClassLevelId)))
            {
                return Result.Failure<FeeNoticeGridDto>(Error.Validation(
                    "fee.level_not_in_section", $"A class on the line \"{line.Label}\" is not an active class of {grid.Section.Name}."));
            }
        }

        var kept = request.Lines.Where(line => line.Id is not null).Select(line => line.Id!.Value).ToHashSet();
        var removed = grid.Labels.Where(label => !kept.Contains(label.Id)).ToList();
        foreach (var label in removed)
        {
            await fees.RemoveLabelAsync(label, cancellationToken).ConfigureAwait(false);
        }

        var labels = new List<FeeLabel>(request.Lines.Count);
        var amounts = grid.Amounts.Where(amount => kept.Contains(amount.FeeLabelId)).ToList();
        var cellsSet = 0;
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            FeeLabel label;
            if (line.Id is { } id)
            {
                label = saved[id];
                var updated = label.Update(line.Label, index + 1, line.ShowOnPortal);
                if (updated.IsFailure)
                {
                    return Result.Failure<FeeNoticeGridDto>(updated.Error);
                }
            }
            else
            {
                var created = FeeLabel.Create(Guid.CreateVersion7(), grid.Section.Id, line.Label, index + 1, line.Kind, line.ShowOnPortal);
                if (created.IsFailure)
                {
                    return Result.Failure<FeeNoticeGridDto>(created.Error);
                }

                label = created.Value;
                await fees.AddLabelAsync(label, cancellationToken).ConfigureAwait(false);
            }

            labels.Add(label);
            foreach (var cell in line.Amounts)
            {
                var current = amounts.Find(amount => amount.FeeLabelId == label.Id && amount.ClassLevelId == cell.ClassLevelId);
                if (cell.Amount is not { } value)
                {
                    if (current is not null)
                    {
                        await fees.RemoveAmountAsync(current, cancellationToken).ConfigureAwait(false);
                        amounts.Remove(current);
                    }

                    continue;
                }

                cellsSet++;
                if (current is not null)
                {
                    var changed = current.Change(value);
                    if (changed.IsFailure)
                    {
                        return Result.Failure<FeeNoticeGridDto>(changed.Error);
                    }

                    continue;
                }

                var added = FeeAmount.Create(Guid.CreateVersion7(), label.Id, grid.Term.Id, cell.ClassLevelId, value);
                if (added.IsFailure)
                {
                    return Result.Failure<FeeNoticeGridDto>(added.Error);
                }

                await fees.AddAmountAsync(added.Value, cancellationToken).ConfigureAwait(false);
                amounts.Add(added.Value);
            }
        }

        // Figures only, never who owes what: the notice is the same for every pupil of a class.
        await auditSink.RecordAsync(
            Privileges.Fee.Manage,
            "fee_notice",
            grid.Section.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["termId"] = grid.Term.Id.ToString("D", CultureInfo.InvariantCulture),
                ["lines"] = labels.Count,
                ["linesRemoved"] = removed.Count,
                ["amountsSet"] = cellsSet,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success((grid with { Labels = labels, Amounts = amounts }).ToDto());
    }
}

/// <summary>What the grid is built from, loaded once for the read and the save.</summary>
internal sealed record FeeNoticeGridContext(
    Section Section,
    Term Term,
    string SessionName,
    Term? PreviousTerm,
    string? PreviousSessionName,
    IReadOnlyList<ClassLevel> Levels,
    IReadOnlyList<FeeLabel> Labels,
    IReadOnlyList<FeeAmount> Amounts)
{
    /// <summary>The grid; a section with no saved lines is offered spec 6.2.13's seed, unsaved.</summary>
    public FeeNoticeGridDto ToDto()
    {
        var isDefault = Labels.Count == 0;
        var lines = isDefault
            ? FeeLabel.Defaults.Select(line => new FeeGridLineDto(null, line.Label, line.Kind, false, [])).ToList()
            : Labels.OrderBy(label => label.DisplayOrder)
                .Select(label => new FeeGridLineDto(
                    label.Id,
                    label.Label,
                    label.Kind,
                    label.ShowOnPortal,
                    [.. Amounts.Where(amount => amount.FeeLabelId == label.Id).Select(amount => new FeeGridAmountDto(amount.ClassLevelId, amount.Amount))]))
                .ToList();
        return new FeeNoticeGridDto(
            Section.Id,
            Section.Name,
            Term.Id,
            Term.Name,
            SessionName,
            PreviousTerm?.Id,
            PreviousTerm is null ? null : $"{PreviousTerm.Name} {PreviousSessionName}".Trim(),
            isDefault,
            [.. Levels.Select(level => new FeeGridLevelDto(level.Id, level.Name))],
            lines,
            Version());
    }

    /// <summary>A hash of the section's lines and this term's amounts; null before any line is saved.</summary>
    public string? Version()
    {
        if (Labels.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var label in Labels.OrderBy(label => label.Id))
        {
            builder.Append(CultureInfo.InvariantCulture, $"L|{label.Id:D}|{label.Label}|{label.DisplayOrder}|{label.Kind}|{label.ShowOnPortal}\n");
        }

        foreach (var amount in Amounts.OrderBy(amount => amount.FeeLabelId).ThenBy(amount => amount.ClassLevelId))
        {
            builder.Append(CultureInfo.InvariantCulture, $"A|{amount.FeeLabelId:D}|{amount.ClassLevelId:D}|{amount.Amount}\n");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}

/// <summary>Loads a <see cref="FeeNoticeGridContext"/>.</summary>
internal sealed class FeeNoticeGridReader(
    ISectionRepository sections,
    IClassLevelRepository classLevels,
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IFeeNoticeRepository fees)
{
    /// <summary>The section's grid for the term; tracked for a save.</summary>
    public async Task<Result<FeeNoticeGridContext>> LoadAsync(Guid sectionId, Guid termId, bool tracked, CancellationToken cancellationToken)
    {
        var section = (await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.Id == sectionId);
        if (section is null)
        {
            return Result.Failure<FeeNoticeGridContext>(Error.NotFound("section.not_found", "No section was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<FeeNoticeGridContext>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var session = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);
        var previous = await fees.FindPreviousTermAsync(term, cancellationToken).ConfigureAwait(false);
        var previousSession = previous is null ? null : await sessions.FindReadOnlyByIdAsync(previous.SessionId, cancellationToken).ConfigureAwait(false);
        var levels = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false))
            .Where(level => level.SectionId == section.Id && level.Status == LevelStatus.Active)
            .OrderBy(level => level.ProgressionOrder)
            .ToList();
        var labels = tracked
            ? await fees.ListLabelsTrackedAsync(section.Id, cancellationToken).ConfigureAwait(false)
            : await fees.ListLabelsReadOnlyAsync(section.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<Guid> labelIds = [.. labels.Select(label => label.Id)];
        var amounts = labels.Count == 0
            ? []
            : tracked
                ? await fees.ListAmountsTrackedAsync(term.Id, labelIds, cancellationToken).ConfigureAwait(false)
                : await fees.ListAmountsReadOnlyAsync(term.Id, labelIds, cancellationToken).ConfigureAwait(false);
        return Result.Success(new FeeNoticeGridContext(section, term, session?.Name ?? string.Empty, previous, previousSession?.Name, levels, labels, amounts));
    }
}
