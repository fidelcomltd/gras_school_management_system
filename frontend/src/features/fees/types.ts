import type { components } from '@/api/schema';

/** Query/mutation keys for the fee notice (spec 6.2.13). A `const` object, not an `enum` (`erasableSyntaxOnly`). */
export const FeesKeys = {
  Grid: 'fees.grid',
  SaveGrid: 'fees.saveGrid',
  Outstanding: 'fees.outstanding',
  SaveOutstanding: 'fees.saveOutstanding',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type FeeNoticeGridDto = components['schemas']['FeeNoticeGridDto'];
export type FeeGridLineDto = components['schemas']['FeeGridLineDto'];
export type FeeLabelKind = components['schemas']['FeeLabelKind'];
export type SaveFeeNoticeGridCommand = components['schemas']['SaveFeeNoticeGridCommand'];
export type OutstandingFeeSheetDto = components['schemas']['OutstandingFeeSheetDto'];
export type SaveOutstandingFeesCommand = components['schemas']['SaveOutstandingFeesCommand'];
