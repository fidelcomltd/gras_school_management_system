using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using SchoolManagement.Application.Portal;
using SchoolManagement.Application.Results.Sheets;

namespace SchoolManagement.Api.Portal;

/// <summary>
/// The portal's server-rendered pages (spec 6.9.2, 6.9.8; human ruling 2026-09-22). Plain HTML with inline CSS and no
/// JavaScript, so it works on any phone browser. Every dynamic value is HTML-encoded. The copy is spec 6.9.4's, verbatim.
/// </summary>
internal static class PortalHtml
{
    private const string Styles =
        "*{box-sizing:border-box}body{margin:0;font:16px/1.5 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1a1a1a;background:#f6f6f4}" +
        "main{max-width:480px;margin:0 auto;padding:20px 16px 40px}header{text-align:center;margin-bottom:20px}header img{width:72px;height:72px;object-fit:contain}" +
        "h1{font-size:20px;margin:8px 0 0}h2{font-size:18px;margin:0 0 8px}label{display:block;font-weight:600;margin:16px 0 6px}" +
        "input{width:100%;min-height:48px;font-size:18px;padding:10px 12px;border:1px solid #767676;border-radius:8px;background:#fff;text-transform:uppercase}" +
        "button,.button{display:block;width:100%;min-height:48px;margin-top:20px;font-size:17px;font-weight:600;border:0;border-radius:8px;background:#1f4e79;color:#fff;text-align:center;text-decoration:none;padding:13px}" +
        ".secondary{background:#fff;color:#1f4e79;border:1px solid #1f4e79}.notice{background:#fff;border:1px solid #d0d0d0;border-left:4px solid #1f4e79;border-radius:8px;padding:14px 16px;margin-bottom:16px}" +
        "ul{list-style:none;padding:0;margin:0}li{margin:8px 0}li a,li span{display:flex;justify-content:space-between;min-height:48px;align-items:center;padding:10px 14px;border-radius:8px;border:1px solid #d0d0d0;background:#fff;color:#1a1a1a;text-decoration:none}" +
        "li span{color:#6b6b6b;background:#efefef}.muted{color:#555;font-size:15px}h3{font-size:16px;margin:20px 0 4px}" +
        ".scroll{overflow-x:auto;background:#fff;border:1px solid #d0d0d0;border-radius:8px}table{border-collapse:collapse;width:100%;font-size:14px}" +
        "th,td{padding:7px 8px;border-bottom:1px solid #e3e3e3;text-align:center}th:first-child,td:first-child{text-align:left}th{background:#f0f0f0;font-weight:600}" +
        "dl{display:grid;grid-template-columns:auto 1fr;gap:4px 12px;margin:0}dt{color:#555}dd{margin:0;font-weight:600}.big{font-size:22px;font-weight:700}" +
        "details{margin-top:16px}summary{min-height:44px;padding:10px 0;font-weight:600;cursor:pointer}.revised{border-left-color:#8a5a00}";

    /// <summary>Scripts blocked outright; the inline stylesheet allowed only by its hash.</summary>
    public static readonly string ContentSecurityPolicy =
        "default-src 'none'; style-src 'sha256-" + Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Styles))) +
        "'; img-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";

    /// <summary>The landing page (spec 6.9.2 step 1), optionally with a message block above the form.</summary>
    public static string Landing(PortalBranding branding, PortalCopy? message, IReadOnlyList<PortalViewingSession> openSessions)
    {
        var body = new StringBuilder();
        if (message is not null)
        {
            body.Append(Notice(message));
        }

        foreach (var session in openSessions)
        {
            body.Append(CultureInfo.InvariantCulture, $"<p class=\"notice\">You are viewing results for <strong>{E(session.PupilName)}</strong>. <a href=\"/portal/terms?u={session.UseId:D}\">Continue</a></p>");
        }

        body.Append("<form method=\"post\" action=\"/portal/lookup\" autocomplete=\"off\">")
            .Append("<label for=\"registrationNumber\">Registration number</label>")
            .Append("<input id=\"registrationNumber\" name=\"registrationNumber\" required maxlength=\"60\" autocapitalize=\"characters\" spellcheck=\"false\">")
            .Append("<label for=\"pin\">Pin</label>")
            .Append("<input id=\"pin\" name=\"pin\" required maxlength=\"40\" autocapitalize=\"characters\" spellcheck=\"false\">")
            .Append("<button type=\"submit\">Check result</button></form>");
        return Page(branding, "Check a result", body.ToString());
    }

    /// <summary>The term selector for one viewing session (spec 6.9.2 step 4), with the other open sessions to switch to.</summary>
    public static string Terms(PortalBranding branding, PortalViewingSession session, IReadOnlyList<PortalViewingSession> others)
    {
        var body = new StringBuilder();
        body.Append(CultureInfo.InvariantCulture, $"<div class=\"notice\"><h2>{E(session.PupilName)}</h2><div class=\"muted\">{E(session.RegistrationNumber)}</div></div>");

        foreach (var group in session.Sessions)
        {
            body.Append(CultureInfo.InvariantCulture, $"<h3>{E(group.SessionName)} session</h3><ul>");
            foreach (var term in group.Terms)
            {
                body.Append(term.Availability switch
                {
                    PortalTermAvailability.Available =>
                        string.Create(CultureInfo.InvariantCulture, $"<li><a href=\"/portal/result/{term.TermId:D}?u={session.UseId:D}\">{E(term.TermName)}<strong>Available</strong></a></li>"),
                    PortalTermAvailability.BeingCorrected =>
                        $"<li><span>{E(term.TermName)}<em>Being corrected</em></span></li>",
                    _ => $"<li><span>{E(term.TermName)}<em>Not yet released</em></span></li>",
                });
            }

            body.Append("<li><span>Annual cumulative<em>After Third Term results</em></span></li></ul>");
        }

        foreach (var other in others)
        {
            body.Append(CultureInfo.InvariantCulture, $"<p><a class=\"button secondary\" href=\"/portal/terms?u={other.UseId:D}\">Switch to {E(other.PupilName)}</a></p>");
        }

        body.Append(CultureInfo.InvariantCulture, $"<a class=\"button secondary\" href=\"/portal?another=1\">Check another pupil</a>");
        body.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">This will use one more of your pin's uses. You have {session.UsesLeft} left.</p>");
        body.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">For security this page closes at {session.ExpiresAt.ToOffset(TimeSpan.FromHours(1)):HH:mm} (30 minutes after you opened it).</p>");
        body.Append("<form method=\"post\" action=\"/portal/end\"><button type=\"submit\" class=\"secondary\">Finish (for shared phones)</button></form>");
        return Page(branding, "Results", body.ToString());
    }

    /// <summary>The result on screen (spec 6.9.2 step 5): one column for a narrow phone, same values as the PDF.</summary>
    public static string Result(PortalBranding branding, ResultSheet sheet, Guid useId)
    {
        var body = new StringBuilder();
        if (sheet.RevisionNotice is { } notice)
        {
            body.Append(CultureInfo.InvariantCulture, $"<div class=\"notice revised\"><strong>{E(notice)}</strong></div>");
        }

        body.Append("<div class=\"notice\"><dl>")
            .Append(CultureInfo.InvariantCulture, $"<dt>Name</dt><dd>{E(sheet.PupilName)}</dd>")
            .Append(CultureInfo.InvariantCulture, $"<dt>Class</dt><dd>{E(sheet.ClassName)}</dd>")
            .Append(CultureInfo.InvariantCulture, $"<dt>Term</dt><dd>{E(sheet.TermName)}, {E(sheet.AcademicYear)}</dd>");
        if (sheet.Age is { } age)
        {
            body.Append(CultureInfo.InvariantCulture, $"<dt>Age</dt><dd>{age}</dd>");
        }

        if (sheet.Instructor is { } instructor)
        {
            body.Append(CultureInfo.InvariantCulture, $"<dt>Instructor</dt><dd>{E(instructor)}</dd>");
        }

        if (sheet.NextTermBegins is { } next)
        {
            body.Append(CultureInfo.InvariantCulture, $"<dt>Next term begins</dt><dd>{next:dd/MM/yyyy}</dd>");
        }

        var averageText = sheet.TermAverage is { } average ? average.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
        body.Append("</dl></div>")
            .Append(CultureInfo.InvariantCulture, $"<div class=\"notice\"><div class=\"muted\">Term average</div><div class=\"big\">{averageText}</div>")
            .Append(CultureInfo.InvariantCulture, $"<div class=\"muted\">Overall grade: <strong>{E(sheet.OverallGrade ?? string.Empty)}</strong></div></div>");

        body.Append("<h3>Subjects</h3><div class=\"scroll\"><table><thead><tr><th>Subject</th>");
        foreach (var column in sheet.Columns)
        {
            body.Append(CultureInfo.InvariantCulture, $"<th>{E(column.Label)}<br><span class=\"muted\">/{column.MaxMark}</span></th>");
        }

        body.Append("<th>Total</th><th>Grade</th></tr></thead><tbody>");
        foreach (var row in sheet.Subjects)
        {
            body.Append(CultureInfo.InvariantCulture, $"<tr><td>{E(row.Subject)}</td>");
            foreach (var cell in row.Cells)
            {
                body.Append(CultureInfo.InvariantCulture, $"<td>{E(cell ?? string.Empty)}</td>");
            }

            body.Append(CultureInfo.InvariantCulture, $"<td><strong>{row.Total}</strong></td><td>{E(row.Grade ?? string.Empty)}</td></tr>");
        }

        body.Append("<tr><th>Grand total</th>");
        foreach (var total in sheet.GrandTotals)
        {
            body.Append(CultureInfo.InvariantCulture, $"<th>{total}</th>");
        }

        body.Append("<th></th></tr></tbody></table></div>");

        if (sheet.Attendance is { } attendance)
        {
            body.Append("<h3>Attendance</h3><div class=\"notice\"><dl>")
                .Append(CultureInfo.InvariantCulture, $"<dt>Times school opened</dt><dd>{attendance.Opened}</dd>")
                .Append(CultureInfo.InvariantCulture, $"<dt>Times present</dt><dd>{attendance.Present}</dd>")
                .Append(CultureInfo.InvariantCulture, $"<dt>Times absent</dt><dd>{attendance.Absent}</dd></dl></div>");
        }

        foreach (var block in sheet.RatingBlocks)
        {
            body.Append(CultureInfo.InvariantCulture, $"<h3>{E(block.Name)}</h3><div class=\"scroll\"><table><tbody>");
            foreach (var item in block.Items)
            {
                var comment = block.HasComments && item.Comment is { } text ? $"<br><span class=\"muted\">{E(text)}</span>" : string.Empty;
                body.Append(CultureInfo.InvariantCulture, $"<tr><td>{E(item.Name)}{comment}</td><td><strong>{E(item.PointCode ?? string.Empty)}</strong></td></tr>");
            }

            body.Append("</tbody></table></div>");
        }

        if (sheet.RatingKey is { } key)
        {
            body.Append(CultureInfo.InvariantCulture, $"<p class=\"muted\">{E(key)}</p>");
        }

        var teacherLabel = sheet.Section == SheetSection.Primary ? "Teacher's report" : "Teacher's comment";
        var headLabel = sheet.Section == SheetSection.Primary ? "Head teacher's report" : "Head teacher's comment";
        body.Append(CultureInfo.InvariantCulture, $"<h3>{teacherLabel}</h3><div class=\"notice\">{E(sheet.TeacherComment ?? string.Empty)}</div>")
            .Append(CultureInfo.InvariantCulture, $"<h3>{headLabel}</h3><div class=\"notice\">{E(sheet.HeadTeacherComment ?? string.Empty)}</div>");

        body.Append("<details><summary>Grade key</summary><div class=\"scroll\"><table><tbody>");
        foreach (var band in sheet.GradeKey)
        {
            body.Append(CultureInfo.InvariantCulture, $"<tr><td>{E(band.Grade)}</td><td>{E(band.Range)}</td><td>{E(band.Word)}</td></tr>");
        }

        body.Append("</tbody></table></div></details>")
            .Append(CultureInfo.InvariantCulture, $"<a class=\"button secondary\" href=\"/portal/terms?u={useId:D}\">Back to terms</a>");
        return Page(branding, $"{sheet.TermName} result", body.ToString());
    }

    /// <summary>A copy block on its own page, e.g. the session-ended copy.</summary>
    public static string Message(PortalBranding branding, PortalCopy copy) => Page(branding, copy.Heading, Notice(copy));

    private static string Notice(PortalCopy copy)
    {
        var action = copy.ButtonText is null ? string.Empty : $"<a class=\"button\" href=\"{E(copy.ButtonHref ?? "/portal")}\">{E(copy.ButtonText)}</a>";
        return $"<div class=\"notice\" role=\"status\"><h2>{E(copy.Heading)}</h2><p>{E(copy.Body)}</p></div>{action}";
    }

    private static string Page(PortalBranding branding, string title, string body)
    {
        var logo = branding.HasLogo ? "<img src=\"/portal/logo\" alt=\"\">" : string.Empty;
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<meta name=\"robots\" content=\"noindex,nofollow\">" +
            $"<title>{E(title)} · {E(branding.SchoolName)}</title><style>{Styles}</style></head><body><main>" +
            $"<header>{logo}<h1>{E(branding.SchoolName)}</h1></header>{body}</main></body></html>";
    }

    private static string E(string value) => HtmlEncoder.Default.Encode(value);
}

/// <summary>One of spec 6.9.4's parent-facing messages.</summary>
/// <param name="Heading">The heading.</param>
/// <param name="Body">The body text.</param>
/// <param name="ButtonText">The button, or null for none.</param>
/// <param name="ButtonHref">Where it goes; the landing page by default.</param>
internal sealed record PortalCopy(string Heading, string Body, string? ButtonText, string? ButtonHref = null)
{
    public static PortalCopy For(PortalLookupResult result) => result.Status switch
    {
        PortalLookupStatus.PinExhausted => new(
            "This pin has been used up",
            $"This pin has been used {result.MaxUses} times, which is the number allowed. Take your slip to the school office and ask for a new one.",
            "Back"),
        PortalLookupStatus.PinSuspended => new(
            "This pin needs to be checked",
            "This pin has been used for several different pupils, so we have paused it. Please take it to the school office and they will sort it out for you.",
            "Back"),
        PortalLookupStatus.PinRevoked => new(
            "This pin is no longer active", "The school has cancelled this pin. Please contact the school office for a new one.", "Back"),
        PortalLookupStatus.PinExpired => new(
            "This pin has expired", $"This pin was for the {result.SessionName} session. Ask the school office for a pin for the current session.", "Back"),
        PortalLookupStatus.NotPublished => new(
            "Results have not been released yet",
            "The school has not released any results for this pupil yet. Please check again after the school tells you results are ready. Your pin has not been used up.",
            "Back"),
        PortalLookupStatus.TooManyTriesDevice => new(
            "Too many tries", "Please wait 30 minutes and try again. If your slip is not working, the school office can check it for you.", null),
        PortalLookupStatus.TooManyTriesPupil => new(
            "Too many tries for this pupil", "Please wait an hour and try again, or ask the school office to check the slip.", null),
        _ => NotFound,
    };

    public static PortalCopy NotFound { get; } = new(
        "We could not find that result",
        "Check the registration number and the pin on your slip and try again. Letters can be mixed up: the pin never contains the letter O or the number 0, " +
        "or the letter I or the number 1. If it still does not work, take the slip to the school office.",
        "Try again");

    public static PortalCopy SessionEnded { get; } = new(
        "Your session has ended",
        "For security, we closed the page after 30 minutes. Enter the registration number and pin again to carry on. This will count as one more use.",
        "Enter details");

    public static PortalCopy CheckAnother(int usesLeft) => new(
        "Check another pupil",
        $"You will need that pupil's registration number. This will use one more of your pin's uses. You have {usesLeft} left.",
        null);

    public static PortalCopy NotReleased(string termName) => new(
        $"{termName} results are not out yet", $"The school has not released {termName} results for this class.", "Back", "/portal/terms");

    public static PortalCopy NoResult { get; } = new(
        "There is no result for this term",
        "This pupil did not have a result recorded for this term. If you think this is a mistake, please contact the school office.",
        "Back",
        "/portal/terms");

    public static PortalCopy BeingCorrected { get; } = new(
        "This result is being corrected",
        "The school has taken this result down to correct it. A corrected copy will be available shortly. Please check again in a day or two.",
        "Back",
        "/portal/terms");

    public static PortalCopy ServerFault { get; } = new(
        "Something went wrong on our side",
        "This is not your fault and your pin has not been used. Please try again in a few minutes.",
        "Try again");
}
