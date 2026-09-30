import { z } from 'zod';
import { INVALID_DATE_MESSAGE, isIsoOrEmpty } from '@/components/ui/date-text';

const date = z.string().refine(isIsoOrEmpty, INVALID_DATE_MESSAGE);

/** `PATCH /api/v1/terms/{id}` (spec 6.3.10). Every field left blank means "unchanged". */
export const editTermSchema = z.object({
  name: z.string(),
  startDate: date,
  endDate: date,
  timesSchoolOpened: z.string(),
  nextResumptionDate: date,
});

export type EditTermFormValues = z.infer<typeof editTermSchema>;

/** `POST /api/v1/terms/{id}/reopen` (spec 6.3.6): reason must be at least 10 characters. */
export const reopenTermSchema = z.object({
  reason: z.string().min(10, 'Give a reason of at least 10 characters.'),
});

export type ReopenTermFormValues = z.infer<typeof reopenTermSchema>;
