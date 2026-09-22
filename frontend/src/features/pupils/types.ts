import type { components } from '@/api/schema';

/** Query/mutation keys for the Pupils feature. A `const` object — `erasableSyntaxOnly` disallows `enum`. */
export const PupilsKeys = {
  List: 'pupils.list',
  Detail: 'pupils.detail',
  Create: 'pupils.create',
  Update: 'pupils.update',
  CorrectNumber: 'pupils.correctNumber',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type PupilDto = components['schemas']['PupilDto'];
export type PupilStatus = components['schemas']['PupilStatus'];
export type PupilSex = components['schemas']['PupilSex'];
export type AdmissionType = components['schemas']['AdmissionType'];
export type CreatePupilCommand = components['schemas']['CreatePupilCommand'];
export type UpdatePupilBiographicalCommand = components['schemas']['UpdatePupilBiographicalCommand'];
export type CorrectRegistrationNumberCommand = components['schemas']['CorrectRegistrationNumberCommand'];

export const PUPIL_STATUSES: readonly PupilStatus[] = ['Pending', 'Active', 'Transferred', 'Withdrawn', 'Graduated'];

export interface PupilsFilters {
  search: string;
  status: PupilStatus | '';
}

/** `SURNAME First Middle`, the order the school writes names in. */
export function pupilName(pupil: Pick<PupilDto, 'surname' | 'firstName' | 'middleName'>): string {
  return [pupil.surname.toUpperCase(), pupil.firstName, pupil.middleName].filter(Boolean).join(' ');
}
