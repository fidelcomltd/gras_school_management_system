using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Handles <see cref="PublishResultSetCommand"/> (spec 6.7.9). It checks each precondition in the spec's order,
/// returning the spec's own block message on the first failure. It then writes the configuration snapshot and
/// moves Approved to Published. Level positions are not re-derived: neither the nursery nor the primary sheet
/// prints a position (§6.7.12 amendment), so the stale-sibling drift (TASK-0071) cannot reach a parent.
/// </summary>
internal sealed class PublishResultSetHandler(
    IResultSetRepository resultSets,
    IArmRepository arms,
    ITermRepository terms,
    IClassLevelRepository classLevels,
    ISectionRepository sections,
    ISchoolProfileRepository schoolProfile,
    IResultRulesRepository resultRules,
    IConfigVersionRepository configVersions,
    IResultSetReadinessEvaluator readinessEvaluator,
    IRequestHandler<GetSettingsQuery, Result<SettingsDto>> settingsQuery,
    Subjects.SubjectsInEffectResolver subjectsInEffect,
    Abstractions.Auth.IAdminAccountRepository adminAccounts,
    IAcademicSessionRepository academicSessions,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<PublishResultSetCommand, Result<PublishResultSetResponse>>
{
    /// <summary>Third Term is the term the annual result and promotion read (spec 6.2.8).</summary>
    private const int ThirdTermOrdinal = 3;

    // Enums as names, so a sheet reprinted years later never depends on enum ordering.
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    /// <inheritdoc />
    public async Task<Result<PublishResultSetResponse>> HandleAsync(PublishResultSetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TASK-0088's lock rule: locked before any check, so a concurrent settings flag either lands first or waits.
        var resultSet = await resultSets.FindTrackedByIdForUpdateAsync(request.ResultSetId, cancellationToken).ConfigureAwait(false);
        if (resultSet is null)
        {
            return Fail(Error.NotFound("result_set.not_found", "No result set was found with that id."));
        }

        if (resultSet.State != ResultSetState.Approved)
        {
            return Fail(Error.Conflict("result_set.not_approved", $"This result set is {resultSet.State} and cannot be published."));
        }

        if (resultSet.NeedsRecompute)
        {
            return Fail(Error.Conflict(
                "result_set.needs_recompute",
                "Settings or marks have changed since the last computation. Run computation again before publishing."));
        }

        var arm = await arms.FindReadOnlyByIdAsync(resultSet.ArmId, cancellationToken).ConfigureAwait(false);
        var term = await terms.FindReadOnlyByIdAsync(resultSet.TermId, cancellationToken).ConfigureAwait(false);
        if (arm is null || term is null)
        {
            return Fail(Error.NotFound("result_set.not_found", "The result set's arm or term no longer exists."));
        }

        var readiness = await readinessEvaluator.EvaluateAsync(arm, term, resultSet, cancellationToken).ConfigureAwait(false);
        var missingHeadRemarks = readiness.Counters.HeadTeacherRemarks.Total - readiness.Counters.HeadTeacherRemarks.Complete;
        if (missingHeadRemarks > 0)
        {
            return Fail(Error.Conflict(
                "result_set.head_teacher_remarks_missing",
                $"{missingHeadRemarks} {(missingHeadRemarks == 1 ? "pupil has" : "pupils have")} no head teacher remark. Add them before publishing."));
        }

        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        if (profile.CurrentLogoGroupId is null)
        {
            return Fail(Error.Conflict(
                "result_set.logo_missing", "The school logo has not been uploaded. Add it in settings before publishing results."));
        }

        if (profile.CurrentSignatureGroupId is null)
        {
            return Fail(Error.Conflict(
                "result_set.signature_missing",
                "A head teacher signature has not been uploaded. Add it in settings before publishing results."));
        }

        if (term.NextResumptionDate is null)
        {
            return Fail(Error.Conflict(
                "result_set.next_resumption_date_missing",
                "Next term's resumption date is not set. Results print it in the footer. Set it on the term before publishing."));
        }

        if (term.TimesSchoolOpened is null)
        {
            return Fail(Error.Conflict(
                "result_set.times_school_opened_missing",
                "Times school opened is not set on this term. Set it on the term before publishing."));
        }

        var rules = await resultRules.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        if (term.Ordinal == ThirdTermOrdinal && rules.RequireCorePass && rules.CoreSubjectIds.Count == 0)
        {
            // Human ruling 2026-09-18: Third Term publication refuses the seeded incomplete state.
            return Fail(Error.Conflict(
                "result_set.core_subjects_missing",
                "Result rules require a pass in core subjects but name none. Choose the core subjects before publishing Third Term results."));
        }

        var settings = await settingsQuery.HandleAsync(new GetSettingsQuery(), cancellationToken).ConfigureAwait(false);
        if (settings.IsFailure)
        {
            return Fail(settings.Error);
        }

        var levels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = levels.FirstOrDefault(candidate => candidate.Id == arm.ClassLevelId);
        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var section = level is null ? null : allSections.FirstOrDefault(candidate => candidate.Id == level.SectionId);

        var latestVersion = await configVersions.ListAsync(beforeVersionNumber: null, pageSize: 1, cancellationToken).ConfigureAwait(false);
        var configVersionId = latestVersion.Items.Count > 0
            ? Guid.Parse(latestVersion.Items[0].Id)
            : (Guid?)null;

        // Appendix C.8 rule 2: a published sheet never reads live settings, so the subject list and the
        // form teacher ("instructor, captured at publication", E.2/F.1) are frozen here too.
        var subjectsResolution = await subjectsInEffect.ResolveAsync(arm.Id, term.Id, cancellationToken).ConfigureAwait(false);
        var subjects = subjectsResolution.IsSuccess
            ? subjectsResolution.Value.OrderBy(subject => subject.DisplayOrder).Select(subject => new { subject.SubjectId, subject.SubjectName, subject.DisplayOrder }).ToList()
            : [];
        var formTeacher = arm.FormTeacherAdminId is { } teacherId
            ? await adminAccounts.FindReadOnlyByIdAsync(teacherId, cancellationToken).ConfigureAwait(false)
            : null;

        var now = timeProvider.GetUtcNow();
        var snapshot = new
        {
            schemaVersion = 1,
            publishedAt = now,
            revisionNumber = resultSet.RevisionNumber + 1,
            configVersionId,
            settings = settings.Value,
            logoUploadGroupId = profile.CurrentLogoGroupId,
            signatureUploadGroupId = profile.CurrentSignatureGroupId,
            level = level is null ? null : new { level.Id, level.Name },
            section = section is null ? null : new { section.Id, section.Name, section.RatesTraits },
            arm = new { arm.Id, displayName = ArmDisplayName.Compose(level?.Name ?? string.Empty, arm.Label) },
            session = new { Id = term.SessionId, Name = (await academicSessions.FindReadOnlyByIdAsync(term.SessionId, cancellationToken).ConfigureAwait(false))?.Name },
            term = new { term.Id, term.Name, term.Ordinal, term.StartDate, term.EndDate, term.NextResumptionDate, term.TimesSchoolOpened },
            pupilCount = resultSet.PupilCount,
            subjects,
            formTeacherName = formTeacher?.StaffName,
        };
        var snapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJson);

        var publishedBy = currentUser.UserId is { } userId ? Guid.Parse(userId) : (Guid?)null;
        var publication = resultSet.Publish(publishedBy, now, snapshotJson, configVersionId);
        if (publication.IsFailure)
        {
            return Fail(publication.Error);
        }

        await resultSets.AddSnapshotAsync(ResultSetSnapshot.For(resultSet), cancellationToken).ConfigureAwait(false);
        await resultSets.IssueVerificationsAsync(resultSet, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Results.Publish,
            "result_set",
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = nameof(ResultSetState.Published),
                ["armId"] = arm.Id,
                ["termId"] = term.Id,
                ["pupilCount"] = resultSet.PupilCount,
                ["configVersionId"] = configVersionId,
                ["revisionNumber"] = resultSet.RevisionNumber,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["state"] = nameof(ResultSetState.Approved),
            }).ConfigureAwait(false);

        var dto = new ResultSetSummaryDto(
            resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute, resultSet.ReturnReason);
        return Result.Success(new PublishResultSetResponse(dto, now, resultSet.RevisionNumber));
    }

    private static Result<PublishResultSetResponse> Fail(Error error) => Result.Failure<PublishResultSetResponse>(error);
}
