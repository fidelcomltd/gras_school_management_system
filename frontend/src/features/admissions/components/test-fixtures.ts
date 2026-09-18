import type { AdmissionQueueRow, AdmissionRecordDto } from '../types';

/** Shared queue-row fixture for this feature's tests. Not a source file — no 180-line pressure. */
export function pupil(overrides: Partial<AdmissionQueueRow> = {}): AdmissionQueueRow {
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
    admission: null,
    levelAppliedFor: 'Primary 2',
    dateApplicationReceived: '2026-08-01',
    missing: ['Declaration (Section I)'],
    ...overrides,
  };
}

/** `GET /api/v1/admissions/{id}` (TASK-0066) fixture — carries the ids the queue row cannot. */
export function admissionRecord(overrides: Partial<AdmissionRecordDto> = {}): AdmissionRecordDto {
  return {
    sessionId: 'session-1',
    dateApplicationReceived: '2026-08-01',
    dateAdmitted: '2026-09-08',
    classAdmittedInto: 'level-1',
    classAdmittedIntoName: 'Primary 2',
    admissionType: 'New',
    admissionTypeNote: null,
    assessmentRequired: false,
    assessmentResultRemarks: null,
    assignedClassTeacher: null,
    declarationName: 'Chinwe Okafor',
    declarationSigned: true,
    declarationDate: '2026-09-08',
    approvedBy: null,
    approvedAt: null,
    headOfSchoolConfirmed: false,
    headOfSchoolName: null,
    ...overrides,
  };
}
