import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Sessions feature. Terms live here too
 * (`features/sessions/`, per TASK-0042's explicit scope) rather than a
 * sibling `features/terms/` the tag-per-folder rule in
 * `src/features/README.md` would otherwise suggest: a term is never
 * managed except as part of its owning session's detail screen, so
 * splitting the folder would only separate code that always changes
 * together. A `const` object, not a TS `enum` — `erasableSyntaxOnly`
 * disallows `enum` (see `features/auth/types.ts`, the precedent this mirrors).
 */
export const SessionsKeys = {
  List: 'sessions.list',
  Detail: 'sessions.detail',
  Create: 'sessions.create',
  UpdateSession: 'sessions.update',
  UpdateTerm: 'sessions.updateTerm',
  OpenTerm: 'sessions.openTerm',
  CloseTerm: 'sessions.closeTerm',
  ReopenTerm: 'sessions.reopenTerm',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type SessionDto = components['schemas']['SessionDto'];
export type SessionDetailDto = components['schemas']['SessionDetailDto'];
export type SessionState = components['schemas']['SessionState'];
export type TermDto = components['schemas']['TermDto'];
export type TermState = components['schemas']['TermState'];
export type CreateSessionCommand = components['schemas']['CreateSessionCommand'];
export type CreateSessionTermInput = components['schemas']['CreateSessionTermInput'];
export type UpdateSessionCommand = components['schemas']['UpdateSessionCommand'];
export type UpdateTermCommand = components['schemas']['UpdateTermCommand'];
export type ReopenTermCommand = components['schemas']['ReopenTermCommand'];
