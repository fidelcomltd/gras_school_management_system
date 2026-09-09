import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Settings feature. A `const` object, not a TS
 * `enum` — `tsconfig.app.json`'s `erasableSyntaxOnly` disallows `enum` (see
 * `features/auth/types.ts`, the precedent this mirrors).
 */
export const SettingsKeys = {
  Get: 'settings.get',
  UpdateIdentity: 'settings.updateIdentity',
} as const;

/**
 * Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts.
 */
export type SettingsDto = components['schemas']['SettingsDto'];
export type SettingsIdentityGroupDto = components['schemas']['SettingsIdentityGroupDto'];
export type UpdateSchoolIdentityCommand = components['schemas']['UpdateSchoolIdentityCommand'];
