/**
 * Every route path in one place. Components link via `paths.*`, never a string
 * literal — a renamed route should be one edit, and a typo should be a type error.
 *
 * Parameterised routes are functions:
 *   students: { detail: (id: string) => `/students/${id}` }
 */
export const paths = {
  root: '/',
  signIn: '/sign-in',
  changePassword: '/account/password',
  settings: '/settings',
  sessions: '/sessions',
  sessionDetail: (id: string) => `/sessions/${id}`,
  classes: '/classes',
  admins: '/admins',
  adminDetail: (id: string) => `/admins/${id}`,
  roles: '/roles',
  arms: '/arms',
  armDetail: (id: string) => `/arms/${id}`,
  admissions: '/admissions',
  admissionFlow: (id: string, step?: number) => `/admissions/${id}${step ? `?step=${step}` : ''}`,
  pupils: '/pupils',
  pupilImport: '/pupils/import',
  incompleteRecords: '/reports/incomplete-records',
  pupilDetail: (id: string) => `/pupils/${id}`,
  subjects: '/subjects',
  subjectMapping: '/subjects/mapping',
  results: '/results',
  marks: '/results/marks',
  classRecords: '/results/class',
  weekly: '/weekly',
  weeklyCompletion: '/weekly/completion',
  pins: '/pins',
  audit: '/audit',
  pinBatch: (id: string) => `/pins/${id}`,
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];
