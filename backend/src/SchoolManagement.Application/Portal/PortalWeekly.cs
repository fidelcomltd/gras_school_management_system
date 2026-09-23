using System.Text.RegularExpressions;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Portal;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Abstractions.Weekly;
using SchoolManagement.Application.Pins;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Weekly;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Portal;

/// <summary>One row of the parent's week list (spec 6.10.9).</summary>
/// <param name="WeekNumber">The week.</param>
/// <param name="StartDate">The Monday.</param>
/// <param name="EndDate">The Friday.</param>
/// <param name="Available">Published and holding at least one note. Otherwise the row shows "Not available", disabled.</param>
/// <param name="Preview">The teacher's comment from the most recent day that has one, cut to one line.</param>
public sealed record PortalWeeklyWeek(int WeekNumber, DateOnly StartDate, DateOnly EndDate, bool Available, string? Preview);

/// <summary>The parent's weekly-report list for a term, or why there is none.</summary>
/// <param name="Status">Shown, or session ended, or no such term for this pupil.</param>
/// <param name="TermName">e.g. First Term.</param>
/// <param name="SessionName">e.g. 2026/2027.</param>
/// <param name="UseId">The session, for links.</param>
/// <param name="Weeks">One row per week of the term.</param>
public sealed record PortalWeeklyList(
    PortalResultStatus Status, string? TermName = null, string? SessionName = null, Guid? UseId = null, IReadOnlyList<PortalWeeklyWeek>? Weeks = null);

/// <summary>One opened week, or why it cannot be opened. <see cref="File"/> is present only for a PDF request.</summary>
/// <param name="Status">Shown, session ended, or not released.</param>
/// <param name="Sheet">Present when shown.</param>
/// <param name="UseId">The session, for links.</param>
/// <param name="File">The A4 sheet, for a PDF request.</param>
public sealed record PortalWeeklyWeekView(PortalResultStatus Status, WeeklySheet? Sheet = null, Guid? UseId = null, PdfFile? File = null);

/// <summary>Spec 6.10.9: the week list for a term, inside an existing viewing session. Consumes no pin use.</summary>
/// <param name="Tokens">From the cookie.</param>
/// <param name="UseId">Which open session.</param>
/// <param name="TermId">The term.</param>
public sealed record GetPortalWeeklyQuery(IReadOnlyList<string> Tokens, Guid? UseId, Guid TermId) : IQuery<Result<PortalWeeklyList>>;

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalWeeklyQueryValidator : AbstractValidator<GetPortalWeeklyQuery>
{
    public GetPortalWeeklyQueryValidator() => RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
}

/// <summary>Spec 6.10.9: one published week on screen. Consumes no pin use.</summary>
/// <param name="Tokens">From the cookie.</param>
/// <param name="UseId">Which open session.</param>
/// <param name="TermId">The term.</param>
/// <param name="WeekNumber">The week.</param>
public sealed record GetPortalWeeklyWeekQuery(IReadOnlyList<string> Tokens, Guid? UseId, Guid TermId, int WeekNumber)
    : IQuery<Result<PortalWeeklyWeekView>>;

/// <summary>Spec 6.10.9: the appendix G PDF of one published week. Downloading consumes no use.</summary>
/// <param name="Tokens">From the cookie.</param>
/// <param name="UseId">Which open session.</param>
/// <param name="TermId">The term.</param>
/// <param name="WeekNumber">The week.</param>
public sealed record GetPortalWeeklyPdfQuery(IReadOnlyList<string> Tokens, Guid? UseId, Guid TermId, int WeekNumber)
    : IQuery<Result<PortalWeeklyWeekView>>;

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalWeeklyWeekQueryValidator : AbstractValidator<GetPortalWeeklyWeekQuery>
{
    public GetPortalWeeklyWeekQueryValidator()
    {
        RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
        RuleFor(query => query.WeekNumber).InclusiveBetween(1, TermWeeks.MaxWeeks);
    }
}

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalWeeklyPdfQueryValidator : AbstractValidator<GetPortalWeeklyPdfQuery>
{
    public GetPortalWeeklyPdfQueryValidator()
    {
        RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
        RuleFor(query => query.WeekNumber).InclusiveBetween(1, TermWeeks.MaxWeeks);
    }
}

/// <summary>Handles <see cref="GetPortalWeeklyQuery"/>.</summary>
internal sealed class GetPortalWeeklyHandler(
    IPortalRepository portal, ITermRepository terms, IAcademicSessionRepository sessions, IWeeklyReportRepository weekly, TimeProvider timeProvider)
    : IRequestHandler<GetPortalWeeklyQuery, Result<PortalWeeklyList>>
{
    private const int PreviewLength = 90;

    /// <inheritdoc />
    public async Task<Result<PortalWeeklyList>> HandleAsync(GetPortalWeeklyQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await PortalSessionResolver.ResolveAsync(portal, request.Tokens, request.UseId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)
            is not { } session)
        {
            return Result.Success(new PortalWeeklyList(PortalResultStatus.SessionEnded));
        }

        var pupilTerms = await portal.ListTermsAsync(session.Use.PupilId, cancellationToken).ConfigureAwait(false);
        if (!pupilTerms.Any(row => row.TermId == request.TermId)
            || await terms.FindReadOnlyByIdAsync(request.TermId, cancellationToken).ConfigureAwait(false) is not { } term)
        {
            return Result.Success(new PortalWeeklyList(PortalResultStatus.NoResult, UseId: session.Use.Id));
        }

        var academicSession = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);
        var reports = (await weekly.ListPupilTermAsync(session.Use.PupilId, term.Id, cancellationToken).ConfigureAwait(false))
            .ToDictionary(report => report.WeekNumber);
        var weeks = TermWeeks.Derive(term.StartDate, term.EndDate)
            .Select(week => (week.Number, week.StartDate, week.EndDate))
            .Concat(reports.Values.Where(report => report.WeekNumber > TermWeeks.Derive(term.StartDate, term.EndDate).Count)
                .Select(report => (Number: report.WeekNumber, report.StartDate, report.EndDate)))
            .Select(week =>
            {
                var report = reports.GetValueOrDefault(week.Number);
                var available = report is { State: WeeklyReportState.Published, HasContent: true };
                return new PortalWeeklyWeek(week.Number, report?.StartDate ?? week.StartDate, report?.EndDate ?? week.EndDate, available,
                    available ? Preview(report!) : null);
            })
            .ToList();

        return Result.Success(new PortalWeeklyList(PortalResultStatus.Shown, term.Name, academicSession?.Name, session.Use.Id, weeks));
    }

    private static string? Preview(WeeklyReportSnapshot report) =>
        report.Days.LastOrDefault(day => day.Get(WeeklyField.TeacherComment) is not null)?.Get(WeeklyField.TeacherComment) is { } comment
            ? comment.Length <= PreviewLength ? comment : string.Concat(comment.AsSpan(0, PreviewLength - 1), "…")
            : null;
}

/// <summary>
/// Handles <see cref="GetPortalWeeklyWeekQuery"/>. A draft or empty week is "not released": nothing renders and no PDF is
/// generated (appendix G.4 rule 4).
/// </summary>
internal sealed class GetPortalWeeklyWeekHandler(
    IPortalRepository portal,
    ITermRepository terms,
    IAcademicSessionRepository sessions,
    IArmRepository arms,
    IClassLevelRepository levels,
    IWeeklyReportRepository weekly,
    ISchoolProfileRepository schoolProfile,
    TimeProvider timeProvider)
    : IRequestHandler<GetPortalWeeklyWeekQuery, Result<PortalWeeklyWeekView>>
{
    /// <inheritdoc />
    public async Task<Result<PortalWeeklyWeekView>> HandleAsync(GetPortalWeeklyWeekQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await PortalSessionResolver.ResolveAsync(portal, request.Tokens, request.UseId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)
            is not { } session)
        {
            return Result.Success(new PortalWeeklyWeekView(PortalResultStatus.SessionEnded));
        }

        var pupilId = session.Use.PupilId;
        var report = (await weekly.ListPupilTermAsync(pupilId, request.TermId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(candidate => candidate.WeekNumber == request.WeekNumber);
        if (report is not { State: WeeklyReportState.Published, HasContent: true }
            || await terms.FindReadOnlyByIdAsync(request.TermId, cancellationToken).ConfigureAwait(false) is not { } term
            || await portal.FindPupilByIdAsync(pupilId, cancellationToken).ConfigureAwait(false) is not { } pupil)
        {
            return Result.Success(new PortalWeeklyWeekView(PortalResultStatus.NotReleased, UseId: session.Use.Id));
        }

        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var className = await ClassNameAsync(report.ArmId, cancellationToken).ConfigureAwait(false);
        var academicSession = await sessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false);
        var sheet = new WeeklySheet(
            string.IsNullOrWhiteSpace(profile.SchoolName) ? "School" : profile.SchoolName,
            pupil.DisplayName, pupil.RegistrationNumber, className, report.WeekNumber, report.StartDate, report.EndDate,
            term.Name, academicSession?.Name ?? string.Empty, report.Days)
        {
            ReportId = report.Id,
            PupilId = pupilId,
            Revision = report.Revision,
            LogoGroupId = profile.CurrentLogoGroupId,
        };
        return Result.Success(new PortalWeeklyWeekView(PortalResultStatus.Shown, sheet, session.Use.Id));
    }

    private async Task<string> ClassNameAsync(Guid armId, CancellationToken cancellationToken)
    {
        if (await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is not { } arm)
        {
            return string.Empty;
        }

        var level = (await levels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.Id == arm.ClassLevelId);
        return ArmDisplayName.Compose(level?.Name ?? string.Empty, arm.Label);
    }
}

/// <summary>
/// Handles <see cref="GetPortalWeeklyPdfQuery"/>. The session and publication checks are the on-screen handler's, so the
/// two can never disagree. The file is cached per report revision, so any edit to the week invalidates it (G.1).
/// </summary>
internal sealed partial class GetPortalWeeklyPdfHandler(
    IRequestHandler<GetPortalWeeklyWeekQuery, Result<PortalWeeklyWeekView>> page,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore imageStore,
    IResultPdfCache cache,
    IWeeklySheetPdfRenderer renderer,
    TimeProvider timeProvider)
    : IRequestHandler<GetPortalWeeklyPdfQuery, Result<PortalWeeklyWeekView>>
{
    /// <inheritdoc />
    public async Task<Result<PortalWeeklyWeekView>> HandleAsync(GetPortalWeeklyPdfQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outcome = await page.HandleAsync(new GetPortalWeeklyWeekQuery(request.Tokens, request.UseId, request.TermId, request.WeekNumber), cancellationToken)
            .ConfigureAwait(false);
        if (outcome.IsFailure || outcome.Value is not { Status: PortalResultStatus.Shown, Sheet: { } sheet } view)
        {
            return outcome;
        }

        // The result-sheet cache keys files by (id, pupil, revision); a weekly report id never collides with a result set id.
        var key = new ResultPdfKey(sheet.ReportId, sheet.PupilId, sheet.Revision);
        if (await cache.GetAsync(key, cancellationToken).ConfigureAwait(false) is not { } pdf)
        {
            var logo = await SnapshotImages.ReadAsync(schoolImages, imageStore, sheet.LogoGroupId, DomainSizeVariant.Size200, cancellationToken)
                .ConfigureAwait(false);
            pdf = renderer.Render(sheet, logo, timeProvider.GetUtcNow());
            await cache.SetAsync(key, pdf, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(view with { File = new PdfFile(FileName(sheet), pdf) });
    }

    /// <summary>Appendix G.1: e.g. <c>GRAS-2026-0041_Week-04_First-Term_2026-2027.pdf</c>.</summary>
    internal static string FileName(WeeklySheet sheet) =>
        $"{Safe(sheet.RegistrationNumber)}_Week-{sheet.WeekNumber:00}_{Safe(sheet.TermName)}_{Safe(sheet.SessionName)}.pdf";

    private static string Safe(string value) => UnsafeRun().Replace(value, "-").Trim('-') is { Length: > 0 } safe ? safe : "weekly";

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex UnsafeRun();
}
