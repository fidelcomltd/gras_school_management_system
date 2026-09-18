import { z } from 'zod';

/**
 * Client-side mirror of the backend's level validation (spec 6.4.2). Where
 * the two diverge the backend is authoritative — its eight chain-rule
 * rejections still surface verbatim through `error.message`, never
 * paraphrased; this only catches the obvious cases before a round trip.
 */
const NAME = z.string().min(2, 'At least 2 characters.').max(40, 'At most 40 characters.');

export const createLevelSchema = z
  .object({
    name: NAME,
    sectionId: z.string().min(1, 'Choose a section.'),
    placement: z.enum(['insertAfter', 'manualOrder']),
    insertAfterLevelId: z.string(),
    progressionOrder: z.string(),
    nextLevelId: z.string(),
  })
  .refine((v) => v.placement !== 'insertAfter' || v.insertAfterLevelId !== '', {
    message: 'Choose the level this new one follows.',
    path: ['insertAfterLevelId'],
  })
  .refine((v) => v.placement !== 'manualOrder' || v.progressionOrder !== '', {
    message: 'Give this level a position.',
    path: ['progressionOrder'],
  });

export type CreateLevelFormValues = z.infer<typeof createLevelSchema>;

export const editLevelSchema = z.object({
  name: z.string(),
  sectionId: z.string(),
  nextLevelId: z.string(),
  progressionOrder: z.string(),
  status: z.enum(['', 'Active', 'Inactive']),
});

export type EditLevelFormValues = z.infer<typeof editLevelSchema>;
