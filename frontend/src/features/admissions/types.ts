import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Admissions feature (TASK-0064), mirroring
 * `features/arms/types.ts`'s shape. A `const` object, not a TS `enum` —
 * `erasableSyntaxOnly` disallows `enum`.
 */
export const AdmissionsKeys = {
  Queue: 'admissions.queue',
  Approve: 'admissions.approve',
  Decline: 'admissions.decline',
} as const;

/**
 * Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts.
 * The queue's own row shape is `PupilDto` (spec 6.5.15): `admission` is
 * `null` on this read path (see `PupilDto.admission`'s own description) —
 * only the three fields the queue lifts onto the row (`levelAppliedFor`,
 * `dateApplicationReceived`, `missing`) are populated alongside the
 * pupil-biographical fields every other read path also returns.
 */
export type AdmissionQueueRow = components['schemas']['PupilDto'];
export type ApproveAdmissionCommand = components['schemas']['ApproveAdmissionCommand'];
export type DeclineAdmissionCommand = components['schemas']['DeclineAdmissionCommand'];
