using System.Text.Json;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Promotion;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Promotion;

/// <summary>What a promotion run would do, read once and shared by the preview and the commit so they can never disagree.</summary>
internal sealed record PromotionPlan(
    AcademicSession Source,
    AcademicSession? Target,
    IReadOnlyList<PromotionBlockerDto> Blockers,
    IReadOnlyList<PromotionRowDto> Rows,
    IReadOnlyList<PromotionTargetArmDto> TargetArms,
    IReadOnlyList<PromotionCoreSubjectDto> CoreSubjects,
    IReadOnlyList<Pupil> Excluded,
    PromotionBatch? CommittedBatch,
    bool CanDecide);

/// <summary>
/// Spec 6.3.7's preconditions, proposals and default arm distribution. Proposals come from the annual result as it was
/// computed (the same outcome the annual sheet shows); a change of rules takes effect by recomputing annual results.
/// </summary>
internal sealed class PromotionPlanner(
    IAcademicSessionRepository sessions,
    ITermRepository terms,
    IArmRepository arms,
    IClassLevelRepository classLevels,
    IPupilRepository pupils,
    IPromotionRepository promotions,
    IResultRulesRepository resultRules,
    ISubjectRepository subjects,
    IEffectivePrivilegeProvider privileges,
    ICurrentUser currentUser)
{
    private const int ThirdTermOrdinal = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The session's promotion plan, into <paramref name="targetSessionId"/> or else the next session by start date.</summary>
    public async Task<Result<PromotionPlan>> PlanAsync(Guid sessionId, Guid? targetSessionId, CancellationToken cancellationToken)
    {
        var source = await sessions.FindReadOnlyByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return Result.Failure<PromotionPlan>(Error.NotFound("session.not_found", "No session was found with that id."));
        }

        var target = targetSessionId is { } chosen
            ? await sessions.FindReadOnlyByIdAsync(chosen, cancellationToken).ConfigureAwait(false)
            : await promotions.FindNextSessionAsync(source.StartDate, cancellationToken).ConfigureAwait(false);
        if (targetSessionId is not null && target is null)
        {
            return Result.Failure<PromotionPlan>(Error.NotFound("promotion.target_session_not_found", "No session was found with that target id."));
        }

        var grants = await privileges.GetGrantsAsync(currentUser.UserId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        var canDecide = PupilAccessGuard.Resolve(grants, Privileges.Promotion.Decide) == PupilAccessScope.SchoolWide;
        var blockers = new List<PromotionBlockerDto>();

        var committed = await promotions.FindCommittedForSessionAsync(source.Id, cancellationToken).ConfigureAwait(false);
        if (committed is not null)
        {
            var into = committed.TargetSessionId == target?.Id
                ? target
                : await sessions.FindReadOnlyByIdAsync(committed.TargetSessionId, cancellationToken).ConfigureAwait(false);

            // Spec 6.3.9's wording, as written.
            blockers.Add(new PromotionBlockerDto(
                "promotion.already_run",
                $"Promotion has already been run for {source.Name}. Reverse the existing batch if you need to run it again."));
            return Result.Success(new PromotionPlan(source, into ?? target, blockers, [], [], [], [], committed, canDecide));
        }

        var sessionTerms = await terms.ListBySessionReadOnlyAsync(source.Id, cancellationToken).ConfigureAwait(false);
        if (!sessionTerms.Any(term => term.Ordinal == ThirdTermOrdinal && term.State == TermState.Closed))
        {
            blockers.Add(new PromotionBlockerDto(
                "promotion.third_term_not_closed", $"Third Term of {source.Name} is not closed. Close it before running promotion."));
        }

        if (target is null)
        {
            blockers.Add(new PromotionBlockerDto(
                "promotion.no_target_session", $"There is no session after {source.Name}. Create the new session before running promotion."));
        }
        else if (target.Id == source.Id || target.StartDate <= source.StartDate)
        {
            blockers.Add(new PromotionBlockerDto(
                "promotion.target_session_not_later", $"Pupils can only be promoted into a later session than {source.Name}."));
        }
        else if (target.State == SessionState.Closed)
        {
            blockers.Add(new PromotionBlockerDto("promotion.target_session_closed", $"{target.Name} is closed. Pupils cannot be promoted into it."));
        }

        var rules = await resultRules.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        if (rules.RequireCorePass && rules.CoreSubjectIds.Count == 0)
        {
            // Human ruling 2026-09-18: never a silent promotion with no core-subject check.
            blockers.Add(new PromotionBlockerDto(
                "promotion.result_rules_incomplete",
                "Result rules are incomplete: a pass in every core subject is required, but no core subjects are chosen. Choose them in "
                + "settings, or turn the requirement off, before running promotion."));
        }

        var allArms = await arms.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false);
        var levels = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(level => level.Id);
        var armsById = allArms.ToDictionary(arm => arm.Id);
        string ArmName(Arm arm) => ArmDisplayName.Compose(levels.TryGetValue(arm.ClassLevelId, out var level) ? level.Name : string.Empty, arm.Label);

        var active = await pupils.ListActiveEnrolledInSessionAsync(source.Id, cancellationToken).ConfigureAwait(false);
        if (active.Count == 0)
        {
            blockers.Add(new PromotionBlockerDto("promotion.no_pupils", $"There are no active pupils in {source.Name} to promote."));
        }

        var annual = (await promotions.ListAnnualResultsAsync(source.Id, cancellationToken).ConfigureAwait(false))
            .GroupBy(result => result.PupilId)
            .ToDictionary(group => group.Key, group => group.First());
        var armsWithResults = annual.Values.Select(result => result.ArmId).ToHashSet();
        foreach (var armId in active.Select(entry => entry.ArmId).Distinct().Where(armId => !armsWithResults.Contains(armId)))
        {
            blockers.Add(new PromotionBlockerDto(
                "promotion.annual_results_missing",
                $"Annual results have not been computed for {ArmName(armsById[armId])}. Compute them before running promotion."));
        }

        var subjectNames = (await subjects.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(subject => subject.Id, subject => subject.Name);
        var coreSubjects = rules.CoreSubjectIds
            .Select(id => new PromotionCoreSubjectDto(id, subjectNames.GetValueOrDefault(id, string.Empty)))
            .ToList();

        var targetArms = target is null
            ? []
            : allArms.Where(arm => arm.SessionId == target.Id && arm.Status == ArmStatus.Active).OrderBy(arm => arm.LabelKey, StringComparer.Ordinal).ToList();
        var enrolledByArm = await promotions.CountOpenByArmAsync([.. targetArms.Select(arm => arm.Id)], cancellationToken).ConfigureAwait(false);
        var targetArmDtos = targetArms
            .Select(arm => new PromotionTargetArmDto(arm.Id, ArmName(arm), arm.ClassLevelId, arm.Capacity, enrolledByArm.GetValueOrDefault(arm.Id)))
            .ToList();

        var drafts = active
            .Select(entry => Draft(entry.Pupil, armsById[entry.ArmId], levels, annual.GetValueOrDefault(entry.Pupil.Id), rules.CoreSubjectIds, rules.PassMark))
            .ToList();

        // Spec 6.3.7: every level that will receive pupils needs an arm. The next level of every level promoted from, and the
        // level itself where a pupil is proposed to repeat.
        var receiving = drafts.Where(draft => draft.NextLevelId is not null).Select(draft => draft.NextLevelId!.Value)
            .Concat(drafts.Where(draft => draft.Proposed == PromotionDecisionOutcome.Repeat).Select(draft => draft.LevelId))
            .Distinct()
            .Where(levelId => levels.ContainsKey(levelId))
            .OrderBy(levelId => levels[levelId].ProgressionOrder);
        if (target is not null)
        {
            foreach (var levelId in receiving.Where(levelId => targetArms.All(arm => arm.ClassLevelId != levelId)))
            {
                blockers.Add(new PromotionBlockerDto(
                    "promotion.receiving_level_without_arm",
                    $"{levels[levelId].Name} has no arm in {target.Name}. Create at least one arm before running promotion."));
            }
        }

        var defaults = DistributeByAverage(drafts, targetArms);
        var rows = drafts
            .OrderBy(draft => draft.ArmName, StringComparer.Ordinal).ThenBy(draft => draft.DisplayName, StringComparer.Ordinal)
            .Select(draft => new PromotionRowDto(
                draft.Pupil.Id,
                draft.DisplayName,
                draft.Pupil.RegistrationNumber,
                draft.ArmId,
                draft.ArmName,
                draft.LevelId,
                draft.NextLevelId,
                draft.Average,
                draft.CoreResults,
                draft.Proposed,
                defaults.GetValueOrDefault(draft.Pupil.Id)))
            .ToList();

        var excluded = await promotions.ListInactiveEnrolledInSessionAsync(source.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PromotionPlan(source, target, blockers, rows, targetArmDtos, coreSubjects, excluded, null, canDecide));
    }

    /// <summary>Surname first, as every register in the product.</summary>
    public static string DisplayName(Pupil pupil) => Weekly.WeeklyProjection.DisplayName(pupil.Surname, pupil.FirstName, pupil.MiddleName);

    /// <summary>The level a decision's pupil goes to: their own on a repeat, the next on promotion, none on graduation.</summary>
    public static Guid? DestinationLevel(PromotionDecisionOutcome outcome, Guid levelId, Guid? nextLevelId) => outcome switch
    {
        PromotionDecisionOutcome.Repeat => levelId,
        PromotionDecisionOutcome.Promoted or PromotionDecisionOutcome.PromotedOnTrial => nextLevelId,
        _ => null,
    };

    private static RowDraft Draft(
        Pupil pupil, Arm arm, Dictionary<Guid, ClassLevel> levels, AnnualResult? result, IReadOnlyList<Guid> coreSubjectIds, int passMark)
    {
        var level = levels.GetValueOrDefault(arm.ClassLevelId);
        var nextLevelId = level?.NextLevelId;
        var subjectsTaken = result is null
            ? []
            : JsonSerializer.Deserialize<List<AnnualSubjectResult>>(result.SubjectsJson, Json) ?? [];
        var core = coreSubjectIds
            .Select(id => subjectsTaken.Find(subject => subject.SubjectId == id) is { } taken
                ? new PromotionCoreResultDto(id, taken.Mean, taken.Mean >= passMark)
                : new PromotionCoreResultDto(id, null, null))
            .ToList();

        // Spec 6.3.9: a terminal-level pupil is proposed Graduated whatever the average; on trial is never proposed.
        PromotionDecisionOutcome? proposed = result is null
            ? null
            : nextLevelId is null
                ? PromotionDecisionOutcome.Graduated
                : result.ProposedOutcome == PromotionOutcome.Repeat ? PromotionDecisionOutcome.Repeat : PromotionDecisionOutcome.Promoted;
        var armName = ArmDisplayName.Compose(level?.Name ?? string.Empty, arm.Label);
        return new RowDraft(pupil, DisplayName(pupil), arm.Id, armName, arm.ClassLevelId, nextLevelId, result?.CumulativeAverage, core, proposed);
    }

    /// <summary>
    /// Spec 6.3.7's default: each destination level's pupils, strongest first, dealt across its arms in turn so every arm gets a
    /// comparable spread of ability. A pupil with no proposal has no default; the administrator chooses both.
    /// </summary>
    private static Dictionary<Guid, Guid> DistributeByAverage(IReadOnlyList<RowDraft> drafts, IReadOnlyList<Arm> targetArms)
    {
        var assigned = new Dictionary<Guid, Guid>();
        var byLevel = drafts
            .Where(draft => draft.Proposed is { } outcome && DestinationLevel(outcome, draft.LevelId, draft.NextLevelId) is not null)
            .GroupBy(draft => DestinationLevel(draft.Proposed!.Value, draft.LevelId, draft.NextLevelId)!.Value);
        foreach (var group in byLevel)
        {
            var levelArms = targetArms.Where(arm => arm.ClassLevelId == group.Key).ToList();
            if (levelArms.Count == 0)
            {
                continue;
            }

            var ordered = group
                .OrderByDescending(draft => draft.Average ?? decimal.MinValue)
                .ThenBy(draft => draft.DisplayName, StringComparer.Ordinal)
                .ToList();
            for (var index = 0; index < ordered.Count; index++)
            {
                assigned[ordered[index].Pupil.Id] = levelArms[index % levelArms.Count].Id;
            }
        }

        return assigned;
    }

    private sealed record RowDraft(
        Pupil Pupil,
        string DisplayName,
        Guid ArmId,
        string ArmName,
        Guid LevelId,
        Guid? NextLevelId,
        decimal? Average,
        IReadOnlyList<PromotionCoreResultDto> CoreResults,
        PromotionDecisionOutcome? Proposed);
}
