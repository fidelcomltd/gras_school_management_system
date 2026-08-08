import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'gra.theme';

function systemPrefersDark(): boolean {
  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') return false;
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

function resolvePreference(preference: ThemePreference): ResolvedTheme {
  if (preference === 'system') return systemPrefersDark() ? 'dark' : 'light';
  return preference;
}

/** The `.dark` class on <html> is what the token layer in semantic.css keys off. */
function applyToDocument(resolved: ResolvedTheme): void {
  if (typeof document === 'undefined') return;
  document.documentElement.classList.toggle('dark', resolved === 'dark');
}

interface ThemeState {
  preference: ThemePreference;
  resolved: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
  /** Light <-> dark. Resolves 'system' to its current value first. */
  toggle: () => void;
  /** Recomputes from the OS setting. Only has an effect while on 'system'. */
  syncWithSystem: () => void;
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set, get) => ({
      preference: 'system',
      resolved: resolvePreference('system'),

      setPreference: (preference) => {
        const resolved = resolvePreference(preference);
        applyToDocument(resolved);
        set({ preference, resolved });
      },

      toggle: () => {
        const next: ThemePreference = get().resolved === 'dark' ? 'light' : 'dark';
        get().setPreference(next);
      },

      syncWithSystem: () => {
        if (get().preference !== 'system') return;
        const resolved = resolvePreference('system');
        applyToDocument(resolved);
        set({ resolved });
      },
    }),
    {
      name: STORAGE_KEY,
      // Only the user's choice is durable. `resolved` is derived on every load,
      // otherwise a stale value would flash before the OS setting is read.
      partialize: (state) => ({ preference: state.preference }),
      onRehydrateStorage: () => (state) => {
        if (!state) return;
        const resolved = resolvePreference(state.preference);
        applyToDocument(resolved);
        state.resolved = resolved;
      },
    },
  ),
);

/**
 * Applies the stored theme and keeps it in step with the OS while the user is
 * on 'system'. Call once, at startup. Returns an unsubscribe function.
 */
export function initTheme(): () => void {
  const { preference, syncWithSystem } = useThemeStore.getState();
  applyToDocument(resolvePreference(preference));

  if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
    return () => undefined;
  }

  const query = window.matchMedia('(prefers-color-scheme: dark)');
  const onChange = () => {
    syncWithSystem();
  };
  query.addEventListener('change', onChange);
  return () => {
    query.removeEventListener('change', onChange);
  };
}
