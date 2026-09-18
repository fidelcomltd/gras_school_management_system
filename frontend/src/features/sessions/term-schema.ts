import { z } from 'zod';

/** `PATCH /api/v1/terms/{id}` (spec 6.3.10). Every field left blank means "unchanged". */
export const editTermSchema = z.object({
  name: z.string(),
  startDate: z.string(),
  endDate: z.string(),
  timesSchoolOpened: z.string(),
  nextResumptionDate: z.string(),
});

export type EditTermFormValues = z.infer<typeof editTermSchema>;

/** `POST /api/v1/terms/{id}/reopen` (spec 6.3.6): reason must be at least 10 characters. */
export const reopenTermSchema = z.object({
  reason: z.string().min(10, 'Give a reason of at least 10 characters.'),
});

export type ReopenTermFormValues = z.infer<typeof reopenTermSchema>;
