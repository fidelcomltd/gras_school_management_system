import { z } from 'zod';

/**
 * Client-side mirror of the backend's identity validation (spec 6.2.3). Where
 * the two diverge the backend is authoritative — a 422 still surfaces through
 * `fieldErrors`, this only catches the obvious cases before a round trip.
 *
 * `motto` is nullable on the wire (`SettingsIdentityGroupDto.motto`,
 * `UpdateSchoolIdentityCommand.motto`) but an `<Input>` always yields a
 * string; the form carries it as a plain (possibly empty) string and
 * `SchoolIdentityForm` maps `''` to `null` only when building the command.
 */
export const schoolIdentitySchema = z.object({
  schoolName: z.string().min(1, 'Enter the school name.'),
  shortName: z.string().min(1, 'Enter a short name.'),
  address: z.string().min(1, 'Enter an address.'),
  phone: z.string().min(1, 'Enter a phone number.'),
  email: z.email({ message: 'Enter a valid email address.' }),
  motto: z.string(),
  headTeacherName: z.string().min(1, "Enter the head teacher's name."),
});

export type SchoolIdentityFormValues = z.infer<typeof schoolIdentitySchema>;
