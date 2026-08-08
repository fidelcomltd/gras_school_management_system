import { create } from 'zustand';
import {
  clearSession,
  onSessionExpired,
  setSession,
  type AuthSession,
} from '@/lib/auth/auth-session';

/**
 * Client-side view of "is someone signed in".
 *
 * The token itself lives in `lib/auth/auth-session` and never enters this
 * store — a Zustand store is inspectable from devtools and easy to
 * accidentally persist. This holds status only.
 *
 * Intentionally has no user profile. The shape of a signed-in user comes from
 * `contracts/openapi.json`, which does not exist yet. The session feature adds
 * it once the backend defines it.
 */

export type SessionStatus = 'anonymous' | 'authenticated' | 'expired';

interface SessionState {
  status: SessionStatus;
  /** Records a successful sign-in and starts the expiry countdown. */
  signIn: (session: AuthSession) => void;
  /**
   * Clears client state. NOT a complete logout on its own — the caller must
   * first revoke server-side, or the session stays alive on the backend.
   */
  signOut: () => void;
  /** Called when the token lapses. Distinct from `signOut` so the UI can explain why. */
  markExpired: () => void;
}

export const useSessionStore = create<SessionState>()((set) => ({
  status: 'anonymous',

  signIn: (session) => {
    setSession(session);
    set({ status: 'authenticated' });
  },

  signOut: () => {
    clearSession();
    set({ status: 'anonymous' });
  },

  markExpired: () => {
    set((state) => (state.status === 'authenticated' ? { status: 'expired' } : state));
  },
}));

/**
 * Bridges the auth module's expiry signal to the store. Call once at startup;
 * returns an unsubscribe function.
 */
export function connectSessionExpiry(): () => void {
  return onSessionExpired(() => {
    useSessionStore.getState().markExpired();
  });
}
