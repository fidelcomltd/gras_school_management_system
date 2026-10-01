import { describe, expect, it } from 'vitest';
import { schoolClock, schoolDateTime } from './date';

describe('school date-times', () => {
  it('reads an instant on the school clock, WAT, the same form as the prints', () => {
    expect(schoolDateTime('2026-09-30T20:58:00Z')).toBe('Sept 30, 2026, 9:58pm WAT');
    expect(schoolDateTime('2026-12-31T23:05:00Z')).toBe('Jan 1, 2027, 12:05am WAT');
    expect(schoolDateTime('2026-06-15T11:00:00+00:00')).toBe('June 15, 2026, 12:00pm WAT');
    expect(schoolClock('2026-03-02T07:09:00Z')).toBe('8:09am WAT');
  });
});
