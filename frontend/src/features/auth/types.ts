import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Auth feature. No inline string keys elsewhere.
 * A `const` object, not a TS `enum` — `tsconfig.app.json`'s `erasableSyntaxOnly`
 * disallows `enum` (it emits runtime code beyond plain type erasure); this
 * gives the identical `AuthKeys.Me` call-site shape without it.
 */
export const AuthKeys = {
  Me: 'auth.me',
  SignIn: 'auth.signIn',
  SignOut: 'auth.signOut',
} as const;

/**
 * Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts.
 * Shared by sign-in, `me`, `refresh` and `password` (approved contract delta §0).
 */
export type AuthSessionResponse = components['schemas']['AuthSessionResponse'];
export type SignInCommand = components['schemas']['SignInCommand'];
