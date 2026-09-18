import { z } from 'zod';

/**
 * Client-side mirror of the backend's session validation (spec 6.3.5). Where
 * the two diverge the backend is authoritative — a 422 still surfaces
 * verbatim through `error.message`/`fieldErrors`, this only catches the
 * obvious cases before a round trip.
 */
const termDates = z.object({
  startDate: z.string().min(1, 'Start date is required.'),
  endDate: z.string().min(1, 'End date is required.'),
  nextResumptionDate: z.string(),
});

export const createSessionSchema = z.object({
  name: z.string().regex(/^\d{4}\/\d{4}$/, 'Use the format YYYY/YYYY.'),
  startDate: z.string().min(1, 'Start date is required.'),
  endDate: z.string().min(1, 'End date is required.'),
  term1: termDates,
  term2: termDates,
  term3: termDates,
});

export type CreateSessionFormValues = z.infer<typeof createSessionSchema>;

export const editSessionSchema = z.object({
  name: z.string().regex(/^\d{4}\/\d{4}$/, 'Use the format YYYY/YYYY.').or(z.literal('')),
  startDate: z.string(),
  endDate: z.string(),
});

export type EditSessionFormValues = z.infer<typeof editSessionSchema>;
