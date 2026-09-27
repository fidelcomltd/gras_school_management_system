import type { PromotionDecisionOutcome, PromotionPreviewDto, PromotionRowDto, PromotionTargetArmDto } from './types';

export type Outcome = NonNullable<PromotionDecisionOutcome>;

/** One row's editable state on the review screen. `outcome` is null until chosen for a pupil with no annual result. */
export interface DraftDecision {
  outcome: Outcome | null;
  targetArmId: string | null;
  reason: string;
}

export type Drafts = Record<string, DraftDecision>;

export const OUTCOME_LABELS: Record<Outcome, string> = {
  Promoted: 'Promoted',
  Repeat: 'Repeat',
  PromotedOnTrial: 'Promoted on trial',
  Graduated: 'Graduated',
};

export const REASON_MIN = 10;

/** The level a pupil goes to under an outcome: their own on a repeat, the next on promotion, none on graduation. */
export function destinationLevel(row: PromotionRowDto, outcome: Outcome | null): string | null {
  if (outcome === 'Repeat') return row.classLevelId;
  if (outcome === 'Promoted' || outcome === 'PromotedOnTrial') return row.nextLevelId ?? null;
  return null;
}

/** Spec 6.3.7: at the terminal level a pupil graduates or repeats; elsewhere on trial is offered only to `promotion.decide`. */
export function outcomeChoices(row: PromotionRowDto, canDecide: boolean): Outcome[] {
  if (row.nextLevelId == null) return ['Graduated', 'Repeat'];
  return canDecide ? ['Promoted', 'Repeat', 'PromotedOnTrial'] : ['Promoted', 'Repeat'];
}

/** Changing a system proposal needs `promotion.decide`; a blank proposal is the administrator's to fill. */
export function canChangeOutcome(row: PromotionRowDto, canDecide: boolean): boolean {
  return canDecide || row.proposedOutcome == null;
}

export function initialDrafts(preview: PromotionPreviewDto): Drafts {
  return Object.fromEntries(
    preview.rows.map((row) => [
      row.pupilId,
      { outcome: row.proposedOutcome ?? null, targetArmId: row.proposedTargetArmId ?? null, reason: '' },
    ]),
  );
}

/** Pupils this promotion would put in each target arm, from the current drafts. */
export function assignedCounts(drafts: Drafts): Map<string, number> {
  const counts = new Map<string, number>();
  for (const draft of Object.values(drafts)) {
    if (draft.targetArmId) counts.set(draft.targetArmId, (counts.get(draft.targetArmId) ?? 0) + 1);
  }
  return counts;
}

/**
 * A new outcome for one row: keep the chosen arm when it still suits the destination level, otherwise the destination
 * arm with the most room left (enrolled plus assigned against capacity), so a change never leaves a blank arm behind.
 */
export function withOutcome(
  drafts: Drafts,
  row: PromotionRowDto,
  outcome: Outcome,
  arms: PromotionTargetArmDto[],
): Drafts {
  const current = drafts[row.pupilId] ?? { outcome: null, targetArmId: null, reason: '' };
  const level = destinationLevel(row, outcome);
  const candidates = arms.filter((arm) => arm.classLevelId === level);
  let targetArmId: string | null = null;
  if (level !== null) {
    if (candidates.some((arm) => arm.armId === current.targetArmId)) {
      targetArmId = current.targetArmId;
    } else {
      const counts = assignedCounts(drafts);
      const room = (arm: PromotionTargetArmDto) => Number(arm.capacity) - Number(arm.enrolledCount) - (counts.get(arm.armId) ?? 0);
      targetArmId = [...candidates].sort((a, b) => room(b) - room(a))[0]?.armId ?? null;
    }
  }

  return { ...drafts, [row.pupilId]: { ...current, outcome, targetArmId } };
}

/** What stops the commit button, row by row: a blank outcome, a missing arm, or an on-trial row without its reason. */
export function rowProblem(row: PromotionRowDto, draft: DraftDecision | undefined): string | null {
  if (!draft?.outcome) return 'Choose an outcome.';
  if (destinationLevel(row, draft.outcome) !== null && !draft.targetArmId) return 'Choose a target arm.';
  if (draft.outcome === 'PromotedOnTrial' && draft.reason.trim().length < REASON_MIN) {
    return `Give a reason of at least ${REASON_MIN} characters.`;
  }
  return null;
}
