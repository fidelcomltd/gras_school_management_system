import { z } from 'zod';

/**
 * Client-side mirror of the backend's arm validation (spec 6.4.3, 6.4.9).
 * Where the two diverge the backend is authoritative — its rejections still
 * surface verbatim through `error.message`, never paraphrased; this only
 * catches the obvious cases before a round trip.
 */
const LABEL = z.string().min(1, 'Give this room a label.').max(16, 'At most 16 characters.');
const CAPACITY = z.string();

export const createArmSchema = z.object({
  sessionId: z.string().min(1, 'Choose a session.'),
  levelId: z.string().min(1, 'Choose a level.'),
  label: LABEL,
  capacity: CAPACITY,
});
export type CreateArmFormValues = z.infer<typeof createArmSchema>;

/**
 * `status` follows `editLevelSchema`'s convention: `''` means "leave
 * unchanged". `Closed` is never offered — spec 6.4.3 says it is set
 * automatically when the owning session closes and cannot be requested here.
 */
export const editArmSchema = z.object({
  label: LABEL,
  capacity: CAPACITY,
  formTeacherAdminId: z.string(),
  status: z.enum(['', 'Active', 'Inactive']),
});
export type EditArmFormValues = z.infer<typeof editArmSchema>;

/** One level's row in the bulk-create form — mirrors `BulkCreateArmsLevelEntry` plus a display name. */
export const bulkCreateArmsRowSchema = z.object({
  levelId: z.string(),
  levelName: z.string(),
  armCount: z
    .string()
    .refine((v) => v === '' || /^\d+$/.test(v), 'Whole number only.'),
  capacity: z
    .string()
    .refine((v) => v === '' || /^\d+$/.test(v), 'Whole number only.'),
});

export const bulkCreateArmsSchema = z.object({
  sessionId: z.string().min(1, 'Choose a session.'),
  rows: z.array(bulkCreateArmsRowSchema),
});
export type BulkCreateArmsFormValues = z.infer<typeof bulkCreateArmsSchema>;
