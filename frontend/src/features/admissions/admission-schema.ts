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
