import type { PupilDto } from './types';

/** Test fixtures for this feature. Not a source file — no 180-line pressure. */
export function pupil(overrides: Partial<PupilDto> = {}): PupilDto {
  return {
    id: 'pupil-1',
    registrationNumber: null,
    surname: 'Okafor',
    firstName: 'Chidera',
    middleName: null,
    sex: 'Female',
    dateOfBirth: '2020-05-03',
    ageYears: 6,
    nationality: 'Nigerian',
    stateOfOrigin: 'Anambra',
    lga: 'Awka South',
    homeAddress: '14 Zik Avenue, Awka',
    previousSchool: null,
    previousClass: null,
    status: 'Pending',
    otherInformation: null,
    matchedField: null,
    createdAtUtc: '2026-08-03T09:30:00+00:00',
    createdBy: null,
    ...overrides,
  };
}
