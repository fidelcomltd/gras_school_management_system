import { z } from 'zod';

/** Mirrors `AuthPolicy` (spec 6.1.11). The server stays authoritative, and also refuses any of the last five passwords. */
export const PASSWORD_MIN_LENGTH = 12;
const PASSWORD_MAX_LENGTH = 128;

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: z
      .string()
      .min(PASSWORD_MIN_LENGTH, `Use at least ${PASSWORD_MIN_LENGTH} characters.`)
      .max(PASSWORD_MAX_LENGTH, `Use at most ${PASSWORD_MAX_LENGTH} characters.`)
      .regex(/\p{L}/u, 'Include at least one letter.')
      .regex(/\p{Nd}/u, 'Include at least one digit.'),
    confirmPassword: z.string(),
  })
  .refine((values) => values.confirmPassword === values.newPassword, {
    path: ['confirmPassword'],
    message: 'The two new passwords do not match.',
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    path: ['newPassword'],
    message: 'Choose a password different from your current one.',
  });

export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;
