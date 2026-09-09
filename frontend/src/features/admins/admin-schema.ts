import { z } from 'zod';

/**
 * Client-side mirror of the backend's admin-account validation (spec 6.1.3).
 * Where the two diverge the backend is authoritative — its own rejection
 * still surfaces verbatim through `error.message`; this only catches the
 * obvious cases before a round trip.
 */
const STAFF_NAME = z.string().min(1, 'Staff name is required.');
const EMAIL = z.string().min(1, 'Email is required.').email('Enter a valid email address.');
const PHONE = z.string().min(1, 'Phone number is required.');

export const createAdminSchema = z.object({
  staffName: STAFF_NAME,
  email: EMAIL,
  phone: PHONE,
});

export type CreateAdminFormValues = z.infer<typeof createAdminSchema>;

export const editAdminSchema = z.object({
  staffName: STAFF_NAME,
  email: EMAIL,
  phone: PHONE,
  isSuperAdmin: z.enum(['', 'true', 'false']),
});

export type EditAdminFormValues = z.infer<typeof editAdminSchema>;

/**
 * `status` is a hardcoded literal union rather than the generated
 * `AdminAccountStatus` re-export, matching `classes/level-schema.ts`'s own
 * `editLevelSchema` precedent — a form values type, not a wire type.
 */
export const changeStatusSchema = z
  .object({
    status: z.enum(['Active', 'Suspended', 'Deactivated']),
    reason: z.string(),
  })
  .refine((v) => v.status !== 'Deactivated' || v.reason.trim().length >= 10, {
    message: 'Give a reason of at least 10 characters.',
    path: ['reason'],
  });

export type ChangeStatusFormValues = z.infer<typeof changeStatusSchema>;
