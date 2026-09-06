import { z } from 'zod';

/**
 * Client-side mirror of the backend's sign-in validation (approved contract
 * delta: `SignInCommandValidator` requires both fields and format-checks
 * `email`). Where the two diverge the backend is authoritative — a 422 from the
 * server still surfaces through `fieldErrors`, this only catches the obvious
 * cases before a round trip.
 */
export const signInSchema = z.object({
  email: z.email({ message: 'Enter a valid email address.' }),
  password: z.string().min(1, 'Enter your password.'),
});

export type SignInFormValues = z.infer<typeof signInSchema>;
