import { z } from 'zod';

/**
 * Client-side mirror of the backend's pupil validation (spec 6.5.3, 6.5.9). The backend is authoritative: a 422 still
 * surfaces through `fieldErrors` or verbatim, this only catches the obvious cases before a round trip.
 */
const required = (label: string) => z.string().trim().min(1, `${label} is required.`);

export const biographicalSchema = z.object({
  surname: required('Surname'),
  firstName: required('First name'),
  middleName: z.string(),
  sex: z.enum(['Male', 'Female'], { message: 'Choose male or female.' }),
  dateOfBirth: required('Date of birth'),
  nationality: z.string(),
  stateOfOrigin: required('State of origin'),
  lga: required('LGA'),
  homeAddress: required('Home address'),
  previousSchool: z.string(),
  previousClass: z.string(),
  otherInformation: z.string(),
});

export type BiographicalFormValues = z.infer<typeof biographicalSchema>;

export const createPupilSchema = biographicalSchema.extend({
  classAdmittedInto: required('Class admitted into'),
  admissionType: z.enum(['New', 'Returning'], { message: 'Choose new or returning.' }),
  dateApplicationReceived: z.string(),
  assessmentRequired: z.boolean(),
});

export type CreatePupilFormValues = z.infer<typeof createPupilSchema>;

export const correctNumberSchema = z.object({
  registrationNumber: required('Registration number'),
  reason: z.string().trim().min(10, 'Give a reason of at least 10 characters.'),
});

export type CorrectNumberFormValues = z.infer<typeof correctNumberSchema>;

/** Optional text inputs are sent as null, not "". */
export function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
