import type { components } from '@/api/schema';

/** Query/mutation keys for the Results feature: mark entry, ratings, attendance, remarks and the workflow. */
export const ResultsKeys = {
  ArmSubjects: 'results.armSubjects',
  ScoreSheet: 'results.scoreSheet',
  SaveScoreSheet: 'results.saveScoreSheet',
  VoidScoreSheet: 'results.voidScoreSheet',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type ScoreSheetDto = components['schemas']['ScoreSheetDto'];
export type ScoreSheetRowDto = components['schemas']['ScoreSheetRowDto'];
export type SaveScoreSheetCommand = components['schemas']['SaveScoreSheetCommand'];
export type VoidScoreSheetCommand = components['schemas']['VoidScoreSheetCommand'];
export type ArmSubjectDto = components['schemas']['ArmSubjectDto'];
export type ResultSetState = components['schemas']['ResultSetState'];

/** Spec 6.7.11: entry is open while Draft or returned for correction (and before the set exists). */
export function isEditable(state: ResultSetState | undefined): boolean {
  return state === undefined || state === 'Draft' || state === 'ReturnedForCorrection';
}

/** How a state reads to staff. */
export const STATE_LABEL: Record<ResultSetState, string> = {
  Draft: 'Draft',
  AwaitingApproval: 'Awaiting approval',
  Approved: 'Approved',
  Published: 'Published',
  ReturnedForCorrection: 'Returned for correction',
  Withdrawn: 'Withdrawn',
};
