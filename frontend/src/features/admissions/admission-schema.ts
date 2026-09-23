import { z } from 'zod';

/**
 * Client-side mirror of the backend's decline validation (spec 6.5.14:
 * "Requires a reason"). Where the two diverge the backend is authoritative —
 * its rejections still surface verbatim through `error.message`, never
 * paraphrased; this only catches the obvious case before a round trip.
 */
export const declineAdmissionSchema = z.object({
  reason: z.string().trim().min(1, 'Give a reason for declining this application.'),
});
export type DeclineAdmissionFormValues = z.infer<typeof declineAdmissionSchema>;

/**
 * `armId` required — chosen from the selector, never typed. `headOfSchool
 * Confirmed` must be `true`: the server rejects `false` outright (TASK-0051),
 * and the UI's own submit button is ALSO disabled while it's off
 * (`ApproveAdmissionForm`), so this refinement is the belt to that braces.
 * `assessmentResultRemarks`/`headOfSchoolName` stay plain strings here —
 * blank is valid product-wise, and the form maps `''` to explicit `null`
 * before sending (the command's own required-but-nullable trap).
 */
export const approveAdmissionSchema = z
  .object({
    armId: z.string().min(1, 'Choose an arm.'),
    assessmentResultRemarks: z.string(),
    headOfSchoolConfirmed: z.boolean(),
    headOfSchoolName: z.string(),
    // Spec 6.5.16: only rendered, and only then required, when the health questions are the one gap left.
    healthOverride: z.boolean(),
    healthOverrideReason: z.string(),
  })
  .refine((values) => values.headOfSchoolConfirmed, {
    message: 'Confirm the head of school signature before approving.',
    path: ['headOfSchoolConfirmed'],
  })
  .refine(
    (values) => !values.healthOverride || (values.healthOverrideReason.trim().length >= 10 && values.healthOverrideReason.trim().length <= 500),
    { message: 'Say why this admission is approved without the health answers, in 10 to 500 characters.', path: ['healthOverrideReason'] },
  );
export type ApproveAdmissionFormValues = z.infer<typeof approveAdmissionSchema>;
