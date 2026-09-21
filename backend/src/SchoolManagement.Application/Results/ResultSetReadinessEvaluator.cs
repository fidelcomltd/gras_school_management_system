using System.Globalization;
using System.Text.Json;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Results;

/// <summary>
/// Builds a <see cref="ResultSetReadinessDto"/> — the ONE place TASK-0088 stage B's completeness gate
/// is computed, called by both <see cref="GetResultSetReadinessHandler"/> and
/// <c>SubmitResultSetHandler</c>, so the submit 422's <c>readiness</c> body is byte-identical to what
/// the GET returns for the same set (contract delta item 2, AC B6) rather than two implementations
/// drifting apart.
/// </summary>
/// <remarks>
/// An INTERFACE, not a plain concrete class like <see cref="SubjectsInEffectResolver"/>, because the
/// integration test proving AC A4's row lock extends to submit needs a DI-replaceable seam it can pause
/// AFTER <c>SubmitResultSetHandler</c> takes its row lock and BEFORE it writes — see
/// <c>ResultSetSubmitEndpointsTests</c>'s racing test.
/// </remarks>
internal interface IResultSetReadinessEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="arm"/>'s readiness for <paramref name="term"/>. <paramref name="resultSet"/>
    /// is <see langword="null"/> for a "Not started" arm — every counter and blocker reads accordingly.
    /// </summary>
    Task<ResultSetReadinessDto> EvaluateAsync(
        Arm arm, Term term, ResultSet? resultSet, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IResultSetReadinessEvaluator"/>
internal sealed class ResultSetReadinessEvaluator(
    IClassLevelRepository classLevels,
    ISectionRepository sections,
    IEnrolmentRepository enrolments,
    SubjectsInEffectResolver subjectsInEffect,
    IAssessmentComponentRepository components,
    ISubjectScoreRepository scores,
    ITraitRepository traits,
    ITraitRatingRepository traitRatings,
    IDevelopmentDomainRepository developmentDomains,
    IDevelopmentRatingRepository developmentRatings,
    IAttendanceEntryRepository attendanceEntries,
    IPupilRemarkRepository remarks,
    IAdminAccountRepository adminAccounts)
    : IResultSetReadinessEvaluator
{
    /// <inheritdoc />
    public async Task<ResultSetReadinessDto> EvaluateAsync(
        Arm arm, Term term, ResultSet? resultSet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arm);
        ArgumentNullException.ThrowIfNull(term);

        var subjectsResolution = await subjectsInEffect.ResolveAsync(arm.Id, term.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ResolvedArmSubject> subjectsInEffectList = subjectsResolution.IsSuccess ? subjectsResolution.Value : [];

        var structure = await components.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var componentCount = structure.Count;
        var nonExamComponentIds = structure.Where(component => !component.IsExamination)
            .Select(component => component.Id)
            .ToHashSet();

        var roster = await enrolments.ListActiveRosterByArmAsync(arm.Id, cancellationToken).ConfigureAwait(false);
        var leftDuringTerm = await enrolments
            .ListLeftDuringTermByArmAsync(arm.Id, term.StartDate, term.EndDate, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<(Guid PupilId, Guid SubjectId), ResultSetMarkSnapshot> marksByPupilSubject = resultSet is null
            ? []
            : (await scores.ListAllActiveReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false))
                .ToDictionary(mark => (mark.PupilId, mark.SubjectId));

        var section = await ResolveSectionAsync(arm, cancellationToken).ConfigureAwait(false);
        var ratingsComplete = await BuildRatingsCompletePredicateAsync(section, resultSet, cancellationToken).ConfigureAwait(false);

        var timesSchoolOpened = term.TimesSchoolOpened;
        Dictionary<Guid, int> presentByPupil = resultSet is null
            ? []
            : (await attendanceEntries.ListReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false))
                .ToDictionary(entry => entry.PupilId, entry => entry.TimesPresent);

        HashSet<Guid> classTeacherRemarkPupilIds = resultSet is null
            ? []
            : (await remarks.ListReadOnlyAsync(resultSet.Id, RemarkKind.ClassTeacher, cancellationToken).ConfigureAwait(false))
                .Select(remark => remark.PupilId)
                .ToHashSet();

        HashSet<Guid> headTeacherRemarkPupilIds = resultSet is null
            ? []
            : (await remarks.ListReadOnlyAsync(resultSet.Id, RemarkKind.HeadTeacher, cancellationToken).ConfigureAwait(false))
                .Select(remark => remark.PupilId)
                .ToHashSet();

        var subjectDtos = subjectsInEffectList
            .Select(subject => new ReadinessSubjectDto(subject.SubjectId.ToString("D", CultureInfo.InvariantCulture), subject.SubjectName))
            .ToArray();

        var pupilRows = new List<ReadinessPupilRowDto>(roster.Count);
        var marksCompleteCells = 0;
        var ratingsCompletePupils = 0;
        var attendanceCompletePupils = 0;
        var classTeacherCompletePupils = 0;
        var headTeacherCompletePupils = 0;

        foreach (var pupil in roster)
        {
            var cells = new List<ReadinessMarkCellDto>(subjectsInEffectList.Count);
            foreach (var subject in subjectsInEffectList)
            {
                var (status, filledParts) = ComputeMarkCell(
                    pupil.PupilId, subject.SubjectId, marksByPupilSubject, nonExamComponentIds, componentCount);
                if (status == MarkCompletionStatus.Complete)
                {
                    marksCompleteCells++;
                }

                cells.Add(new ReadinessMarkCellDto(subject.SubjectId.ToString("D", CultureInfo.InvariantCulture), status, filledParts));
            }

            var pupilRatingsComplete = ratingsComplete(pupil.PupilId);
            if (pupilRatingsComplete)
            {
                ratingsCompletePupils++;
            }

            var pupilAttendanceComplete = presentByPupil.TryGetValue(pupil.PupilId, out var present)
                && timesSchoolOpened is { } opened
                && present <= opened;
            if (pupilAttendanceComplete)
            {
                attendanceCompletePupils++;
            }

            var classTeacherPresent = classTeacherRemarkPupilIds.Contains(pupil.PupilId);
            if (classTeacherPresent)
            {
                classTeacherCompletePupils++;
            }

            var headTeacherPresent = headTeacherRemarkPupilIds.Contains(pupil.PupilId);
            if (headTeacherPresent)
            {
                headTeacherCompletePupils++;
            }

            pupilRows.Add(new ReadinessPupilRowDto(
                pupil.PupilId.ToString("D", CultureInfo.InvariantCulture),
                pupil.RegistrationNumber,
                ComposeDisplayName(pupil.Surname, pupil.FirstName, pupil.MiddleName),
                cells,
                pupilRatingsComplete,
                pupilAttendanceComplete,
                classTeacherPresent,
                headTeacherPresent));
        }

        var leftDuringTermDtos = leftDuringTerm
            .Select(pupil => new ReadinessLeftDuringTermPupilDto(
                pupil.PupilId.ToString("D", CultureInfo.InvariantCulture),
                pupil.RegistrationNumber,
                ComposeDisplayName(pupil.Surname, pupil.FirstName, pupil.MiddleName),
                pupil.LeftOn))
            .ToArray();

        var marksCounter = new ReadinessCounterDto(marksCompleteCells, roster.Count * subjectsInEffectList.Count);
        var ratingsCounter = new ReadinessCounterDto(ratingsCompletePupils, roster.Count);
        var attendanceCounter = new ReadinessCounterDto(attendanceCompletePupils, roster.Count);
        var classTeacherCounter = new ReadinessCounterDto(classTeacherCompletePupils, roster.Count);
        var headTeacherCounter = new ReadinessCounterDto(headTeacherCompletePupils, roster.Count);
        var counters = new ReadinessCountersDto(marksCounter, ratingsCounter, attendanceCounter, classTeacherCounter, headTeacherCounter);

        var formTeacherMissing = await IsFormTeacherMissingAsync(arm, cancellationToken).ConfigureAwait(false);

        var blockers = BuildBlockers(
            marksCounter, ratingsCounter, attendanceCounter, classTeacherCounter,
            resultSet, formTeacherMissing, term, ratesTraits: section.RatesTraits);

        var canSubmit = blockers.Count == 0
            && resultSet is not null
            && resultSet.State is ResultSetState.Draft or ResultSetState.ReturnedForCorrection;

        var resultSetDto = resultSet is null
            ? null
            : new ResultSetSummaryDto(resultSet.Id.ToString("D", CultureInfo.InvariantCulture), resultSet.State, resultSet.NeedsRecompute);

        return new ResultSetReadinessDto(
            arm.Id.ToString("D", CultureInfo.InvariantCulture),
            term.Id.ToString("D", CultureInfo.InvariantCulture),
            resultSetDto,
            subjectDtos,
            componentCount,
            pupilRows,
            leftDuringTermDtos,
            counters,
            blockers,
            canSubmit);
    }

    private static List<ReadinessBlockerDto> BuildBlockers(
        ReadinessCounterDto marksCounter,
        ReadinessCounterDto ratingsCounter,
        ReadinessCounterDto attendanceCounter,
        ReadinessCounterDto classTeacherCounter,
        ResultSet? resultSet,
        bool formTeacherMissing,
        Term term,
        bool ratesTraits)
    {
        var blockers = new List<ReadinessBlockerDto>();

        if (marksCounter.Complete < marksCounter.Total)
        {
            blockers.Add(new ReadinessBlockerDto(
                "marks_incomplete",
                $"{marksCounter.Total - marksCounter.Complete} of {marksCounter.Total} mark cells are still missing."));
        }

        if (ratingsCounter.Complete < ratingsCounter.Total)
        {
            var noun = ratesTraits ? "trait rating" : "development rating";
            blockers.Add(new ReadinessBlockerDto(
                "ratings_incomplete",
                $"{ratingsCounter.Total - ratingsCounter.Complete} of {ratingsCounter.Total} pupils have an incomplete {noun}."));
        }

        if (attendanceCounter.Complete < attendanceCounter.Total)
        {
            blockers.Add(new ReadinessBlockerDto(
                "attendance_incomplete",
                $"{attendanceCounter.Total - attendanceCounter.Complete} of {attendanceCounter.Total} pupils are missing attendance."));
        }

        if (classTeacherCounter.Complete < classTeacherCounter.Total)
        {
            blockers.Add(new ReadinessBlockerDto(
                "class_teacher_remarks_incomplete",
                $"{classTeacherCounter.Total - classTeacherCounter.Complete} of {classTeacherCounter.Total} pupils are missing a class teacher's remark."));
        }

        if (resultSet is null || resultSet.ComputedAtUtc is null)
        {
            blockers.Add(new ReadinessBlockerDto(
                "not_computed",
                "Computation has not been run for this result set. Run computation before submitting."));
        }

        if (resultSet is not null && resultSet.NeedsRecompute)
        {
            // Verbatim spec 6.7.12 edge case wording.
            blockers.Add(new ReadinessBlockerDto(
                "needs_recompute",
                "Marks have changed since the last computation. Run computation again before submitting."));
        }

        if (formTeacherMissing)
        {
            blockers.Add(new ReadinessBlockerDto(
                "form_teacher_missing",
                "This arm has no form teacher with an active account. Assign one before submitting."));
        }

        if (term.TimesSchoolOpened is null)
        {
            blockers.Add(new ReadinessBlockerDto(
                "times_school_opened_missing",
                $"{term.Name}'s times school opened has not been set. Set it before submitting."));
        }

        return blockers;
    }

    private async Task<bool> IsFormTeacherMissingAsync(Arm arm, CancellationToken cancellationToken)
    {
        if (arm.FormTeacherAdminId is not { } formTeacherId)
        {
            return true;
        }

        var account = await adminAccounts.FindReadOnlyByIdAsync(formTeacherId, cancellationToken).ConfigureAwait(false);
        return account is null || account.Status != AdminAccountStatus.Active;
    }

    private async Task<Section> ResolveSectionAsync(Arm arm, CancellationToken cancellationToken)
    {
        var allLevels = await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var level = allLevels.Single(candidate => candidate.Id == arm.ClassLevelId);

        var allSections = await sections.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        return allSections.Single(candidate => candidate.Id == level.SectionId);
    }

    /// <summary>
    /// R1-A: builds a per-pupil "every applicable rating is present" predicate — trait-shaped for a
    /// section that rates traits, development-indicator-shaped otherwise (spec §6.7.12 amendment: the
    /// two shapes are mutually exclusive per <c>Section.RatesTraits</c>, never both).
    /// </summary>
    private async Task<Func<Guid, bool>> BuildRatingsCompletePredicateAsync(
        Section section, ResultSet? resultSet, CancellationToken cancellationToken)
    {
        if (section.RatesTraits)
        {
            var allTraits = await traits.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
            var activeTraitIds = allTraits.Where(trait => trait.Status == TraitStatus.Active)
                .Select(trait => trait.Id)
                .ToHashSet();

            var ratedByPupil = resultSet is null
                ? new Dictionary<Guid, HashSet<Guid>>()
                : (await traitRatings.ListReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false))
                    .GroupBy(rating => rating.PupilId)
                    .ToDictionary(group => group.Key, group => group.Select(rating => rating.TraitId).ToHashSet());

            return pupilId => activeTraitIds.All(traitId =>
                ratedByPupil.TryGetValue(pupilId, out var rated) && rated.Contains(traitId));
        }

        var allDomains = await developmentDomains.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var activeIndicatorIds = allDomains
            .Where(domain => domain.SectionId == section.Id && domain.Status == DevelopmentDomainStatus.Active)
            .SelectMany(domain => domain.Indicators.Where(indicator => indicator.Status == DevelopmentIndicatorStatus.Active))
            .Select(indicator => indicator.Id)
            .ToHashSet();

        var ratedIndicatorsByPupil = resultSet is null
            ? new Dictionary<Guid, HashSet<Guid>>()
            : (await developmentRatings.ListReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false))
                .GroupBy(rating => rating.PupilId)
                .ToDictionary(group => group.Key, group => group.Select(rating => rating.IndicatorId).ToHashSet());

        return pupilId => activeIndicatorIds.All(indicatorId =>
            ratedIndicatorsByPupil.TryGetValue(pupilId, out var rated) && rated.Contains(indicatorId));
    }

    private static (MarkCompletionStatus Status, int FilledParts) ComputeMarkCell(
        Guid pupilId,
        Guid subjectId,
        Dictionary<(Guid PupilId, Guid SubjectId), ResultSetMarkSnapshot> marksByPupilSubject,
        HashSet<Guid> nonExamComponentIds,
        int componentCount)
    {
        if (!marksByPupilSubject.TryGetValue((pupilId, subjectId), out var mark))
        {
            return (MarkCompletionStatus.Empty, 0);
        }

        var componentMarks = JsonSerializer.Deserialize<Dictionary<string, int?>>(mark.ComponentMarksJson)
            ?? new Dictionary<string, int?>(StringComparer.Ordinal);

        var filledComponents = 0;
        foreach (var componentId in nonExamComponentIds)
        {
            var key = componentId.ToString("D", CultureInfo.InvariantCulture);
            if (componentMarks.TryGetValue(key, out var value) && value is not null)
            {
                filledComponents++;
            }
        }

        var examFilled = mark.ExamMark is not null || mark.ExamAbsent;
        var filledParts = filledComponents + (examFilled ? 1 : 0);

        var status = filledParts == componentCount
            ? MarkCompletionStatus.Complete
            : filledParts == 0
                ? MarkCompletionStatus.Empty
                : MarkCompletionStatus.Partial;

        return (status, filledParts);
    }

    private static string ComposeDisplayName(string surname, string firstName, string? middleName) =>
        string.IsNullOrWhiteSpace(middleName) ? $"{surname} {firstName}" : $"{surname} {firstName} {middleName}";
}
