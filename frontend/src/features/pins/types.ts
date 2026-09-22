import type { components } from '@/api/schema';

/** Query/mutation keys for the Pins feature. */
export const PinsKeys = {
  Batches: 'pins.batches',
  Batch: 'pins.batch',
  Generate: 'pins.generate',
  Action: 'pins.action',
  Download: 'pins.download',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type PinBatchDto = components['schemas']['PinBatchDto'];
export type PinBatchDetailDto = components['schemas']['PinBatchDetailDto'];
export type PinSummaryDto = components['schemas']['PinSummaryDto'];
export type GeneratePinBatchCommand = components['schemas']['GeneratePinBatchCommand'];
export type PinBatchState = components['schemas']['PinBatchState'];

export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  return date.toLocaleString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Africa/Lagos' });
}
