using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Reports;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Reports;

/// <summary>A report's filters, bound from the query string. Every property is an id, a code or a number: never free text.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "A marker: it selects the report and documents what a filter may hold.")]
public interface IReportFilters;

/// <summary>A report, on screen. The filters' type selects the report.</summary>
/// <param name="Filters">From the query string.</param>
public sealed record GetReportQuery(IReportFilters Filters) : IQuery<Result<ReportDto>>;

/// <summary>A report as CSV or PDF (spec 15: <c>report.export</c>, and every export audited). A command because it audits.</summary>
/// <param name="Filters">From the query string; their type selects the report.</param>
/// <param name="Format"><c>csv</c> or <c>pdf</c>.</param>
public sealed record ExportReportCommand(IReportFilters Filters, string? Format) : ICommand<Result<ReportFile>>;

/// <summary>Filters are always present; each report checks its own (a missing arm is a 422 naming it).</summary>
internal sealed class GetReportQueryValidator : AbstractValidator<GetReportQuery>
{
    public GetReportQueryValidator() => RuleFor(query => query.Filters).NotNull();
}

/// <summary>csv or pdf, in any case.</summary>
internal sealed class ExportReportCommandValidator : AbstractValidator<ExportReportCommand>
{
    public ExportReportCommandValidator()
    {
        RuleFor(command => command.Filters).NotNull();
        RuleFor(command => command.Format)
            .Must(format => format?.Trim().ToUpperInvariant() is "CSV" or "PDF")
            .WithMessage("format must be csv or pdf.");
    }
}

/// <summary>The arms a caller's grant covers for a report: null for all of them.</summary>
/// <param name="Arms">The granted arms, or null when school-wide.</param>
public sealed record ReportScope(IReadOnlySet<Guid>? Arms)
{
    /// <summary>Everything.</summary>
    public static ReportScope SchoolWide { get; } = new((IReadOnlySet<Guid>?)null);

    /// <summary>Whether the caller may see <paramref name="armId"/>.</summary>
    public bool Allows(Guid armId) => Arms is null || Arms.Contains(armId);
}

/// <summary>What a report is built for: the caller's scope and the moment it is generated.</summary>
/// <param name="Scope">The arms the caller may see.</param>
/// <param name="Now">The generation time.</param>
internal sealed record ReportContext(ReportScope Scope, DateTimeOffset Now);

/// <summary>One report's content. Registered once per report; <see cref="FiltersType"/> routes a request to it.</summary>
internal interface IReportBuilder
{
    /// <summary>The route name, also the audit entity id.</summary>
    string Key { get; }

    /// <summary>What the caller must hold to see it (spec 15's table).</summary>
    string ViewPrivilege { get; }

    /// <summary>The filters this report takes.</summary>
    Type FiltersType { get; }

    /// <summary>The report, or why not: a validation error for bad filters, 403 for an arm outside the scope.</summary>
    Task<Result<ReportDto>> BuildAsync(IReportFilters filters, ReportContext context, CancellationToken cancellationToken);
}

/// <summary>The typed side of <see cref="IReportBuilder"/>, and the helpers every report shares.</summary>
/// <typeparam name="TFilters">The report's filters.</typeparam>
internal abstract class ReportBuilder<TFilters> : IReportBuilder
    where TFilters : IReportFilters
{
    public abstract string Key { get; }

    public virtual string ViewPrivilege => Privileges.Report.View;

    public Type FiltersType => typeof(TFilters);

    public Task<Result<ReportDto>> BuildAsync(IReportFilters filters, ReportContext context, CancellationToken cancellationToken) =>
        BuildAsync((TFilters)filters, context, cancellationToken);

    protected abstract Task<Result<ReportDto>> BuildAsync(TFilters filters, ReportContext context, CancellationToken cancellationToken);

    /// <summary>The report assembled from its parts; <see cref="ReportDto.RowCount"/> counts the data rows.</summary>
    protected ReportDto Report(
        ReportContext context,
        string title,
        IReadOnlyList<string> filters,
        IReadOnlyList<ReportColumnDto> columns,
        IReadOnlyList<ReportRowDto> rows,
        IReadOnlyList<string>? notes = null,
        ReportOrientation orientation = ReportOrientation.Portrait,
        bool twoUp = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(Key, title, filters, orientation, twoUp, columns, rows, notes ?? [], rows.Count(row => row.Kind == ReportRowKind.Data), context.Now);
    }

    protected static Error OutOfScope() => Error.Forbidden("report.forbidden", "You do not have access to that class's records.");

    protected static Error NotFound(string message) => Error.NotFound("report.not_found", message);

    /// <summary>Parses a required id filter, or a 422 naming it.</summary>
    protected static bool TryId(string? value, string name, out Guid id, out Error error)
    {
        error = Error.None;
        if (Guid.TryParse(value, out id))
        {
            return true;
        }

        error = Error.Validation("report.filter", $"{name} must be a valid identifier.");
        return false;
    }

    /// <summary>Parses an optional id filter: true when absent or valid.</summary>
    protected static bool TryOptionalId(string? value, string name, out Guid? id, out Error error)
    {
        id = null;
        error = Error.None;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (Guid.TryParse(value, out var parsed))
        {
            id = parsed;
            return true;
        }

        error = Error.Validation("report.filter", $"{name} must be a valid identifier.");
        return false;
    }
}

/// <summary>
/// What both report handlers share: finding the report for a filters type, resolving the caller's scope, and the audited
/// export. Routes check only that the caller is signed in; the privilege and its arm scope are checked here, because a route
/// with no arm cannot see an arm-restricted grant.
/// </summary>
internal sealed class ReportServices(
    IEnumerable<IReportBuilder> builders,
    IEffectivePrivilegeProvider privileges,
    ICurrentUser currentUser,
    ISchoolProfileRepository schoolProfiles,
    IReportPdfRenderer renderer,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
{
    /// <summary>The audit action every export writes.</summary>
    public const string ExportAuditAction = "report.export";

    private readonly Dictionary<Type, IReportBuilder> _builders = builders.ToDictionary(builder => builder.FiltersType);

    /// <summary>Builds the report for <paramref name="filters"/>, as the caller may see it; exporting also needs <c>report.export</c>.</summary>
    public async Task<Result<(IReportBuilder Builder, ReportDto Report)>> BuildAsync(IReportFilters filters, bool export, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var builder = _builders[filters.GetType()];
        var scope = await ResolveScopeAsync(builder.ViewPrivilege, export, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            var needed = export ? $"{builder.ViewPrivilege} and {Privileges.Report.Export}" : builder.ViewPrivilege;
            return Result.Failure<(IReportBuilder, ReportDto)>(Error.Forbidden("report.forbidden", $"You do not hold {needed} for this report."));
        }

        var built = await builder.BuildAsync(filters, new ReportContext(scope, timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return built.IsFailure ? Result.Failure<(IReportBuilder, ReportDto)>(built.Error) : Result.Success((builder, built.Value));
    }

    /// <summary>The file, after writing the export's audit event (spec 15: the report, the filters and the row count).</summary>
    public async Task<ReportFile> ExportAsync(ReportDto report, bool pdf, IReportFilters filters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(filters);
        byte[] content;
        if (pdf)
        {
            var profile = await schoolProfiles.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
            content = renderer.Render(report, profile.SchoolName);
        }
        else
        {
            content = ReportCsv.Write(report);
        }

        // Filters are ids, codes and numbers only (IReportFilters), so they are safe to keep.
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["format"] = pdf ? "pdf" : "csv",
            ["rows"] = report.RowCount,
        };
        foreach (var property in filters.GetType().GetProperties())
        {
            if (property.GetValue(filters) is { } value)
            {
                metadata[$"filter.{property.Name}"] = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        await auditSink.RecordAsync(ExportAuditAction, "report", report.Key, metadata, currentUser.UserId, cancellationToken).ConfigureAwait(false);
        var stamp = report.GeneratedAtUtc.ToOffset(TimeSpan.FromHours(1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new ReportFile($"{report.Key}_{stamp}.{(pdf ? "pdf" : "csv")}", pdf ? "application/pdf" : "text/csv", content);
    }

    // The view privilege's scope, narrowed to the caller's report.export arms when exporting; null when either is missing.
    private async Task<ReportScope?> ResolveScopeAsync(string viewPrivilege, bool export, CancellationToken cancellationToken)
    {
        var grants = await privileges.GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        var view = Scope(grants, viewPrivilege);
        if (view is null || !export)
        {
            return view;
        }

        var exporting = Scope(grants, Privileges.Report.Export);
        return exporting is null ? null
            : view.Arms is null ? exporting
            : exporting.Arms is null ? view
            : new ReportScope(view.Arms.Where(exporting.Arms.Contains).ToHashSet());
    }

    private static ReportScope? Scope(IReadOnlyCollection<PrivilegeGrant> grants, string privilege) =>
        PupilAccessGuard.Resolve(grants, privilege) switch
        {
            PupilAccessScope.SchoolWide => ReportScope.SchoolWide,
            PupilAccessScope.ArmRestricted => new ReportScope(PupilAccessGuard.ResolveArmIds(grants, privilege)),
            _ => null,
        };
}

/// <summary>Handles <see cref="GetReportQuery"/>.</summary>
internal sealed class GetReportHandler(ReportServices services) : IRequestHandler<GetReportQuery, Result<ReportDto>>
{
    /// <inheritdoc />
    public async Task<Result<ReportDto>> HandleAsync(GetReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var built = await services.BuildAsync(request.Filters, export: false, cancellationToken).ConfigureAwait(false);
        return built.IsFailure ? Result.Failure<ReportDto>(built.Error) : Result.Success(built.Value.Report);
    }
}

/// <summary>Handles <see cref="ExportReportCommand"/>: the same report, as a file, audited.</summary>
internal sealed class ExportReportHandler(ReportServices services) : IRequestHandler<ExportReportCommand, Result<ReportFile>>
{
    /// <inheritdoc />
    public async Task<Result<ReportFile>> HandleAsync(ExportReportCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pdf = string.Equals(request.Format?.Trim(), "pdf", StringComparison.OrdinalIgnoreCase);
        var built = await services.BuildAsync(request.Filters, export: true, cancellationToken).ConfigureAwait(false);
        return built.IsFailure
            ? Result.Failure<ReportFile>(built.Error)
            : Result.Success(await services.ExportAsync(built.Value.Report, pdf, request.Filters, cancellationToken).ConfigureAwait(false));
    }
}
