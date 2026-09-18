import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Arms feature (TASK-0045), mirroring
 * `features/sessions/types.ts`'s shape. A `const` object, not a TS `enum` —
 * `erasableSyntaxOnly` disallows `enum`.
 */
export const ArmsKeys = {
  List: 'arms.list',
  Detail: 'arms.detail',
  NextLabel: 'arms.nextLabel',
  Create: 'arms.create',
  BulkCreate: 'arms.bulkCreate',
  Update: 'arms.update',
  Delete: 'arms.delete',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type ArmDto = components['schemas']['ArmDto'];
export type ArmStatus = components['schemas']['ArmStatus'];
export type CreateArmCommand = components['schemas']['CreateArmCommand'];
export type UpdateArmCommand = components['schemas']['UpdateArmCommand'];
export type BulkCreateArmsCommand = components['schemas']['BulkCreateArmsCommand'];
export type BulkCreateArmsLevelEntry = components['schemas']['BulkCreateArmsLevelEntry'];
export type BulkCreateArmsResponse = components['schemas']['BulkCreateArmsResponse'];
export type NextArmLabelResponse = components['schemas']['NextArmLabelResponse'];
