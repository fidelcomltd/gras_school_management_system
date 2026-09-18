import { z } from 'zod';

/** `POST /api/v1/sections` / `PATCH /api/v1/sections/{id}` (spec 6.4.9): 2..40 characters. */
export const sectionNameSchema = z.object({
  name: z.string().min(2, 'At least 2 characters.').max(40, 'At most 40 characters.'),
});

export type SectionNameFormValues = z.infer<typeof sectionNameSchema>;
