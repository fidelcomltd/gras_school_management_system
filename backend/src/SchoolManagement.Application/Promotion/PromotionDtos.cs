using SchoolManagement.Domain.Promotion;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Promotion;

/// <summary>
/// Spec 6.3.7's review screen: every active pupil with the system's proposal and a default target arm, the destination
/// arms with their counts, and whatever blocks the run. When a committed batch already exists the rows are empty and
/// <paramref name="CommittedBatch"/> describes it, so the screen can offer reversal instead.
/// </summary>
/// <param name="SourceSession">The session promoted from.</param>
/// <param name="TargetSession">The session promoted into; null when none exists yet.</param>
/// <param name="Blockers">Every unmet precondition; commit is refused while any remains.</param>
/// <param name="Rows">One per active pupil, ordered by current arm then name.</param>
/// <param name="TargetArms">The target session's active arms, with capacity and the pupils already enrolled in each.</param>
/// <param name="CoreSubjects">The core subjects the rows' core results are reported against, in settings order.</param>
/// <param name="Excluded">Pupils enrolled in the session who are no longer active (spec 6.3.9): shown apart, never promoted.</param>
/// <param name="CommittedBatch">The committed batch for this session, when promotion has already run.</param>
/// <param name="CanDecide">Whether the caller holds <c>promotion.decide</c>: changing a proposal and on trial need it.</param>
public sealed record PromotionPreviewDto(
    PromotionSessionDto SourceSession,
    PromotionSessionDto? TargetSession,
    IReadOnlyList<PromotionBlockerDto> Blockers,
    IReadOnlyList<PromotionRowDto> Rows,
    IReadOnlyList<PromotionTargetArmDto> TargetArms,
    IReadOnlyList<PromotionCoreSubjectDto> CoreSubjects,
    IReadOnlyList<PromotionExcludedPupilDto> Excluded,
    PromotionBatchDto? CommittedBatch,
    bool CanDecide);

/// <summary>A session as the promotion screen names it.</summary>
/// <param name="Id">The session.</param>
/// <param name="Name">For example 2027/2028.</param>
/// <param name="StartDate">The new enrolments start on the target's start date.</param>
/// <param name="EndDate">The old enrolments close on the source's end date.</param>
public sealed record PromotionSessionDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>One unmet precondition, with spec 6.3.7's wording where it gives one.</summary>
/// <param name="Code">Stable, for example <c>promotion.receiving_level_without_arm</c>.</param>
/// <param name="Message">Shown as written.</param>
public sealed record PromotionBlockerDto(string Code, string Message);

/// <summary>One pupil on the review screen.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="DisplayName">Surname first.</param>
/// <param name="RegistrationNumber">As issued.</param>
/// <param name="CurrentArmId">The arm the pupil ends the session in.</param>
/// <param name="CurrentArmName">For example Primary 2A.</param>
/// <param name="ClassLevelId">The current level: a repeat goes to an arm of this level.</param>
/// <param name="NextLevelId">The level promotion moves to; null at the terminal level, where promotion is graduation.</param>
/// <param name="AnnualAverage">The annual cumulative average; null when no annual result exists (the administrator must choose).</param>
/// <param name="CoreResults">One per core subject, in <see cref="PromotionPreviewDto.CoreSubjects"/> order.</param>
/// <param name="ProposedOutcome">Promoted, Repeat or Graduated; null with no annual result. Never PromotedOnTrial.</param>
/// <param name="ProposedTargetArmId">The balanced round-robin default; null for a graduate or a blank proposal.</param>
public sealed record PromotionRowDto(
    Guid PupilId,
    string DisplayName,
    string? RegistrationNumber,
    Guid CurrentArmId,
    string CurrentArmName,
    Guid ClassLevelId,
    Guid? NextLevelId,
    decimal? AnnualAverage,
    IReadOnlyList<PromotionCoreResultDto> CoreResults,
    PromotionDecisionOutcome? ProposedOutcome,
    Guid? ProposedTargetArmId);

/// <summary>A pupil's annual mean in one core subject.</summary>
/// <param name="SubjectId">The core subject.</param>
/// <param name="Mean">Null when the pupil did not take it (which never counts against them).</param>
/// <param name="Passed">At or above the pass mark; null when not taken.</param>
public sealed record PromotionCoreResultDto(Guid SubjectId, decimal? Mean, bool? Passed);

/// <summary>A core subject's name for the screen's column headings.</summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="Name">As configured.</param>
public sealed record PromotionCoreSubjectDto(Guid SubjectId, string Name);

/// <summary>A destination arm in the target session.</summary>
/// <param name="ArmId">The arm.</param>
/// <param name="Name">For example Primary 3B.</param>
/// <param name="ClassLevelId">Its level.</param>
/// <param name="Capacity">Over capacity warns, never blocks (spec 6.4.9).</param>
/// <param name="EnrolledCount">Pupils already enrolled in it, before this promotion adds any.</param>
public sealed record PromotionTargetArmDto(Guid ArmId, string Name, Guid ClassLevelId, int Capacity, int EnrolledCount);

/// <summary>A pupil enrolled in the session who is no longer active.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="DisplayName">Surname first.</param>
/// <param name="RegistrationNumber">As issued.</param>
/// <param name="Status">Transferred, withdrawn or graduated.</param>
public sealed record PromotionExcludedPupilDto(Guid PupilId, string DisplayName, string? RegistrationNumber, PupilStatus Status);

/// <summary>A promotion batch and its tallies.</summary>
/// <param name="Id">The batch.</param>
/// <param name="SourceSessionId">Promoted from.</param>
/// <param name="TargetSessionId">Promoted into.</param>
/// <param name="TargetSessionName">For the screen's summary line.</param>
/// <param name="State">Committed or Reversed.</param>
/// <param name="CommittedAtUtc">When it was committed.</param>
/// <param name="ReversedAtUtc">When it was reversed, if it was.</param>
/// <param name="Promoted">Pupils promoted.</param>
/// <param name="Repeated">Pupils repeating.</param>
/// <param name="PromotedOnTrial">Pupils promoted on trial.</param>
/// <param name="Graduated">Pupils graduated.</param>
public sealed record PromotionBatchDto(
    Guid Id,
    Guid SourceSessionId,
    Guid TargetSessionId,
    string TargetSessionName,
    PromotionBatchState State,
    DateTimeOffset CommittedAtUtc,
    DateTimeOffset? ReversedAtUtc,
    int Promoted,
    int Repeated,
    int PromotedOnTrial,
    int Graduated)
{
    /// <summary>The batch's tallies.</summary>
    internal static PromotionBatchDto From(PromotionBatch batch, string targetSessionName)
    {
        int Count(PromotionDecisionOutcome outcome) => batch.Decisions.Count(decision => decision.Outcome == outcome);
        return new PromotionBatchDto(
            batch.Id,
            batch.SourceSessionId,
            batch.TargetSessionId,
            targetSessionName,
            batch.State,
            batch.CommittedAtUtc,
            batch.ReversedAtUtc,
            Count(PromotionDecisionOutcome.Promoted),
            Count(PromotionDecisionOutcome.Repeat),
            Count(PromotionDecisionOutcome.PromotedOnTrial),
            Count(PromotionDecisionOutcome.Graduated));
    }
}
