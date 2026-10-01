using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Audit;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Reports.Admin;

/// <summary>Pin distribution and usage filters.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="ArmId">One arm.</param>
/// <param name="BatchId">One batch.</param>
/// <param name="State">Pins in one state only (<c>Unused</c>, <c>Active</c>, <c>Exhausted</c>, <c>Suspended</c>, <c>Revoked</c>).</param>
public sealed record PinUsageFilters(string? SessionId, string? ArmId, string? BatchId, string? State) : IReportFilters;

/// <summary>Audit report filters.</summary>
/// <param name="From">First day (yyyy-MM-dd, Lagos).</param>
/// <param name="To">Last day, inclusive.</param>
/// <param name="ActorAdminId">One actor.</param>
/// <param name="Action">An action code, e.g. <c>result.score.enter</c>, or the start of any part of one (<c>score</c>).</param>
/// <param name="EntityType">One entity type, e.g. <c>subject_score</c>.</param>
/// <param name="Outcome">Success or Rejected.</param>
public sealed record AuditReportFilters(string? From, string? To, string? ActorAdminId, string? Action, string? EntityType, string? Outcome) : IReportFilters;

/// <summary>Settings change history filters.</summary>
/// <param name="From">First day (yyyy-MM-dd, Lagos).</param>
/// <param name="To">Last day, inclusive.</param>
/// <param name="Group">One settings group.</param>
public sealed record SettingsHistoryFilters(string? From, string? To, string? Group) : IReportFilters;

/// <summary>Day filters on the Lagos calendar.</summary>
internal static class ReportDays
{
    /// <summary>Parses optional yyyy-MM-dd bounds into UTC instants: from the start of <paramref name="from"/> to the end of <paramref name="to"/>.</summary>
    public static bool TryRange(string? from, string? to, out DateTimeOffset? fromUtc, out DateTimeOffset? toUtc, out Error error)
    {
        fromUtc = null;
        toUtc = null;
        error = Error.None;
        if (!TryDay(from, out var first) || !TryDay(to, out var last))
        {
            error = Error.Validation("report.filter", "from and to must be dates written yyyy-MM-dd.");
            return false;
        }

        if (first is { Year: < 2000 or > 2999 } || last is { Year: < 2000 or > 2999 })
        {
            error = Error.Validation("report.filter", "from and to must be dates between 2000 and 2999.");
            return false;
        }

        if (first is { } start && last is { } end && end < start)
        {
            error = Error.Validation("report.filter", "to must not be before from.");
            return false;
        }

        fromUtc = first is { } day ? Start(day) : null;
        toUtc = last is { } final ? Start(final.AddDays(1)).AddTicks(-1) : null;
        return true;
    }

    public static string When(DateTimeOffset instant) =>
        instant.ToOffset(WeeklyProjection.LagosOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    // Midnight in Lagos, as UTC: the database stores and compares UTC instants only.
    private static DateTimeOffset Start(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), WeeklyProjection.LagosOffset).ToUniversalTime();

    private static bool TryDay(string? value, out DateOnly? day)
    {
        day = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            day = parsed;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Spec 15 section 10, pin distribution and usage (<c>pin.usage.view</c>): one row per arm of how many pupils the portal was
/// used for, with the pins that opened them and their state, and the last use; the session's batches in the notes. Pins are
/// unbound (any pin opens any number), so a pin belongs to the classes it was used for, not to one class.
/// </summary>
internal sealed class PinUsageReport(IReportReader reader) : ReportBuilder<PinUsageFilters>
{
    public override string Key => "pin-usage";

    public override string ViewPrivilege => Privileges.Pin.UsageView;

    protected override async Task<Result<ReportDto>> BuildAsync(PinUsageFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!TryId(filters.SessionId, "sessionId", out var sessionId, out var error)
            || !TryOptionalId(filters.ArmId, "armId", out var armId, out error)
            || !TryOptionalId(filters.BatchId, "batchId", out var batchId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var stateName = Enum.GetNames<PinState>().FirstOrDefault(name => string.Equals(name, filters.State, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filters.State) && stateName is null)
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", $"state must be one of {string.Join(", ", Enum.GetNames<PinState>())}."));
        }

        if (armId is { } wanted && !context.Scope.Allows(wanted))
        {
            return Result.Failure<ReportDto>(OutOfScope());
        }

        var session = await reader.FindSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Result.Failure<ReportDto>(NotFound("No session was found with that id."));
        }

        var arms = (await reader.ListArmsAsync(sessionId, cancellationToken).ConfigureAwait(false))
            .Where(arm => (armId is null || arm.ArmId == armId) && context.Scope.Allows(arm.ArmId))
            .ToList();
        var (batches, allPins, allUses) = await reader.ListPinUsageAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (batchId is { } chosen && batches.All(batch => batch.BatchId != chosen))
        {
            return Result.Failure<ReportDto>(NotFound("No pin batch of that session has that id."));
        }

        var state = stateName is null ? (PinState?)null : Enum.Parse<PinState>(stateName);
        var pins = allPins.Where(pin => (batchId is null || pin.BatchId == batchId) && (state is null || pin.State == state)).ToDictionary(pin => pin.PinId);
        var uses = allUses.Where(use => pins.ContainsKey(use.PinId)).ToList();
        var register = (await reader.ListRegisterAsync(sessionId, cancellationToken).ConfigureAwait(false)).ToList();
        var armOf = register.ToDictionary(pupil => pupil.PupilId, pupil => pupil.ArmId);

        var rows = arms.ConvertAll(arm =>
        {
            var armUses = uses.Where(use => armOf.GetValueOrDefault(use.PupilId) == arm.ArmId).ToList();
            var armPins = armUses.Select(use => pins[use.PinId]).DistinctBy(pin => pin.PinId).ToList();
            return new ReportRowDto(ReportRowKind.Data,
            [
                arm.Name,
                ReportText.Number(register.Count(pupil => pupil.ArmId == arm.ArmId && pupil.Status == PupilStatus.Active)),
                ReportText.Number(armUses.Select(use => use.PupilId).Distinct().Count()),
                ReportText.Number(armPins.Count),
                ReportText.Number(armPins.Count(pin => pin.State == PinState.Exhausted)),
                ReportText.Number(armPins.Count(pin => pin.State == PinState.Revoked)),
                armUses.Count == 0 ? null : ReportDays.When(armUses.Max(use => use.OpenedAtUtc)),
            ]);
        });

        var inScope = context.Scope.Arms is null ? batches.Where(batch => batchId is null || batch.BatchId == batchId).ToList() : [];
        var notes = inScope.Select(batch =>
        {
            var batchPins = pins.Values.Where(pin => pin.BatchId == batch.BatchId).ToList();
            return $"{batch.Name}: {batchPins.Count} pins, {batchPins.Count(pin => pin.UseCount > 0)} used at least once, " +
                $"{batchPins.Count(pin => pin.State == PinState.Exhausted)} exhausted, {batchPins.Count(pin => pin.State == PinState.Revoked)} revoked.";
        }).ToList();
        notes.Add("Pins open any registration number, so a class's pins are those used to open its pupils' results. Per-pin detail is on each batch's page.");
        if (context.Scope.Arms is not null)
        {
            notes.Add("Batch totals cover every class, so they show only to a school-wide holder.");
        }

        var filterLines = new List<string> { $"Session: {session.Name}" };
        if (state is not null)
        {
            filterLines.Add($"Pins: {state}");
        }

        return Result.Success(Report(
            context,
            "Pin distribution and usage",
            filterLines,
            [
                new("Class", ReportAlign.Left),
                new("Pupils", ReportAlign.Right),
                new("Results opened", ReportAlign.Right),
                new("Pins used", ReportAlign.Right),
                new("Exhausted", ReportAlign.Right),
                new("Revoked", ReportAlign.Right),
                new("Last use", ReportAlign.Left),
            ],
            rows,
            notes));
    }
}

/// <summary>
/// Spec 15 section 10, audit report (<c>audit.view</c>): the filtered audit log for reading and export, newest first, with the
/// before and after values for score changes. At most <see cref="MaxRows"/> rows; narrow the filters for more.
/// </summary>
internal sealed class AuditReport(IAuditEventQueryRepository events, IReportReader reader) : ReportBuilder<AuditReportFilters>
{
    public const int MaxRows = 2_000;

    public override string Key => "audit";

    public override string ViewPrivilege => Privileges.Audit.View;

    /// <summary>Spec 6.1.12: exporting the audit log needs <c>audit.export</c>, whichever route it leaves by.</summary>
    public override string? ExtraExportPrivilege => Privileges.Audit.Export;

    protected override async Task<Result<ReportDto>> BuildAsync(AuditReportFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!ReportDays.TryRange(filters.From, filters.To, out var fromUtc, out var toUtc, out var error)
            || !TryOptionalId(filters.ActorAdminId, "actorAdminId", out var actorId, out error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var outcomeName = Enum.GetNames<AuditOutcome>().FirstOrDefault(name => string.Equals(name, filters.Outcome, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filters.Outcome) && outcomeName is null)
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", $"outcome must be one of {string.Join(", ", Enum.GetNames<AuditOutcome>())}."));
        }

        var outcome = outcomeName is null ? (AuditOutcome?)null : Enum.Parse<AuditOutcome>(outcomeName);
        var action = string.IsNullOrWhiteSpace(filters.Action) ? null : filters.Action.Trim();
        var entityType = string.IsNullOrWhiteSpace(filters.EntityType) ? null : filters.EntityType.Trim();

        var rows = new List<ReportRowDto>();
        var truncated = false;
        await foreach (var item in events.StreamAsync(fromUtc, toUtc, actorId, action, entityType, null, outcome, cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count == MaxRows)
            {
                truncated = true;
                break;
            }

            var score = string.Equals(item.EntityType, "subject_score", StringComparison.Ordinal);
            rows.Add(new(ReportRowKind.Data,
            [
                ReportDays.When(item.OccurredAtUtc),
                item.ActorLabel,
                item.Action,
                item.EntityId is null ? item.EntityType : $"{item.EntityType} {item.EntityId}",
                item.Outcome.ToString(),
                item.Reason,
                score ? item.BeforeJson : null,
                score ? item.AfterJson : null,
            ]));
        }

        var filterLines = new List<string>();
        if (filters.From is not null || filters.To is not null)
        {
            filterLines.Add($"From {filters.From ?? "the start"} to {filters.To ?? "today"}");
        }

        if (actorId is { } actor)
        {
            var names = await reader.FindAdminNamesAsync([actor], cancellationToken).ConfigureAwait(false);
            filterLines.Add($"Actor: {names.GetValueOrDefault(actor, "an admin account")}");
        }

        filterLines.AddRange(new[] { ("Action matching", action), ("Entity matching", entityType), ("Outcome", outcomeName) }
            .Where(entry => entry.Item2 is not null)
            .Select(entry => $"{entry.Item1}: {entry.Item2}"));

        var notes = new List<string> { "Before and after values are shown for score changes; every other change is on the audit log itself." };
        if (truncated)
        {
            notes.Add($"Only the newest {MaxRows.ToString(CultureInfo.InvariantCulture)} events are shown. Narrow the dates or filters, or export the audit log itself.");
        }

        return Result.Success(Report(
            context,
            "Audit report",
            filterLines,
            [
                new("When (WAT)", ReportAlign.Left),
                new("Actor", ReportAlign.Left),
                new("Action", ReportAlign.Left),
                new("Entity", ReportAlign.Left),
                new("Outcome", ReportAlign.Left),
                new("Reason", ReportAlign.Left),
                new("Before", ReportAlign.Left),
                new("After", ReportAlign.Left),
            ],
            rows,
            notes,
            ReportOrientation.Landscape));
    }
}

/// <summary>
/// Spec 15 section 10, settings change history (<c>audit.view</c>): every configuration version with its actor, time, reason and
/// a plain-language summary of what changed against the version before it. How the school answers why a grade looks different
/// from last year.
/// </summary>
internal sealed class SettingsHistoryReport(IReportReader reader) : ReportBuilder<SettingsHistoryFilters>
{
    public override string Key => "settings-history";

    public override string ViewPrivilege => Privileges.Audit.View;

    protected override async Task<Result<ReportDto>> BuildAsync(SettingsHistoryFilters filters, ReportContext context, CancellationToken cancellationToken)
    {
        if (!ReportDays.TryRange(filters.From, filters.To, out var fromUtc, out var toUtc, out var error))
        {
            return Result.Failure<ReportDto>(error);
        }

        var groupName = Enum.GetNames<ConfigVersionGroup>().FirstOrDefault(name => string.Equals(name, filters.Group, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filters.Group) && groupName is null)
        {
            return Result.Failure<ReportDto>(Error.Validation("report.filter", $"group must be one of {string.Join(", ", Enum.GetNames<ConfigVersionGroup>())}."));
        }

        var group = groupName is null ? (ConfigVersionGroup?)null : Enum.Parse<ConfigVersionGroup>(groupName);
        var versions = await reader.ListConfigVersionsAsync(cancellationToken).ConfigureAwait(false);
        var shown = versions
            .Select((version, index) => (Version: version, Previous: index == 0 ? null : versions[index - 1]))
            .Where(entry => (fromUtc is null || entry.Version.CreatedAtUtc >= fromUtc) && (toUtc is null || entry.Version.CreatedAtUtc <= toUtc)
                && (group is null || entry.Version.Group == group))
            .Reverse()
            .ToList();
        var actors = await reader.FindAdminNamesAsync(
                [.. shown.Select(entry => Guid.TryParse(entry.Version.ActorAdminId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct()],
                cancellationToken)
            .ConfigureAwait(false);

        var rows = shown.ConvertAll(entry => new ReportRowDto(ReportRowKind.Data,
        [
            ReportDays.When(entry.Version.CreatedAtUtc),
            Words(entry.Version.Group),
            Guid.TryParse(entry.Version.ActorAdminId, out var actor) ? actors.GetValueOrDefault(actor, "Unknown admin") : "System",
            entry.Version.Reason,
            string.Join("; ", ReportJsonDiff.Describe(entry.Previous?.SnapshotJson, entry.Version.SnapshotJson)),
        ]));

        var filterLines = new List<string>();
        if (filters.From is not null || filters.To is not null)
        {
            filterLines.Add($"From {filters.From ?? "the start"} to {filters.To ?? "today"}");
        }

        if (group is { } chosen)
        {
            filterLines.Add($"Group: {Words(chosen)}");
        }

        return Result.Success(Report(
            context,
            "Settings change history",
            filterLines,
            [
                new("When (WAT)", ReportAlign.Left),
                new("Group", ReportAlign.Left),
                new("By", ReportAlign.Left),
                new("Reason", ReportAlign.Left),
                new("What changed", ReportAlign.Left),
            ],
            rows,
            ["Newest first. Each change is against the configuration saved just before it."],
            ReportOrientation.Landscape));
    }

    private static string Words(ConfigVersionGroup group) => group switch
    {
        ConfigVersionGroup.RegistrationNumber => "Registration numbers",
        ConfigVersionGroup.ResultRules => "Result rules",
        ConfigVersionGroup.RatingScales => "Rating scales",
        ConfigVersionGroup.DevelopmentDomains => "Development domains",
        _ => group.ToString(),
    };
}
