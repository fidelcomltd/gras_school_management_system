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
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];
