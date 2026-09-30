import { z } from 'zod';
import { INVALID_DATE_MESSAGE, isIsoOrEmpty } from '@/components/ui/date-text';

/** A date field's value: ISO, or '' when empty. Badly typed text is refused rather than lost. */
const date = z.string().refine(isIsoOrEmpty, INVALID_DATE_MESSAGE);
const requiredDate = z.string().min(1, 'Start date is required.').pipe(date);

/**
 * Client-side mirror of the backend's session validation (spec 6.3.5). Where
 * the two diverge the backend is authoritative — a 422 still surfaces
 * verbatim through `error.message`/`fieldErrors`, this only catches the
 * obvious cases before a round trip.
 */
const termDates = z.object({
  startDate: requiredDate,
  endDate: z.string().min(1, 'End date is required.').pipe(date),
  nextResumptionDate: date,
});

export const createSessionSchema = z.object({
  name: z.string().regex(/^\d{4}\/\d{4}$/, 'Use the format YYYY/YYYY.'),
  startDate: requiredDate,
  endDate: z.string().min(1, 'End date is required.').pipe(date),
  term1: termDates,
  term2: termDates,
  term3: termDates,
});

export type CreateSessionFormValues = z.infer<typeof createSessionSchema>;

export const editSessionSchema = z.object({
  name: z.string().regex(/^\d{4}\/\d{4}$/, 'Use the format YYYY/YYYY.').or(z.literal('')),
  startDate: date,
  endDate: date,
});

export type EditSessionFormValues = z.infer<typeof editSessionSchema>;
