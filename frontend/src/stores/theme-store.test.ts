import { beforeEach, describe, expect, it, vi } from 'vitest';
import { initTheme, useThemeStore } from './theme-store';

const isDark = () => document.documentElement.classList.contains('dark');

function stubSystemPreference(dark: boolean, listeners: { change?: () => void } = {}) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({
      matches: dark,
      media: '(prefers-color-scheme: dark)',
      addEventListener: (_event: string, handler: () => void) => {
        listeners.change = handler;
      },
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  );
}

beforeEach(() => {
  vi.unstubAllGlobals();
  useThemeStore.setState({ preference: 'system', resolved: 'light' });
  document.documentElement.classList.remove('dark');
});

describe('setPreference', () => {
  it('adds the dark class on the document root', () => {
    useThemeStore.getState().setPreference('dark');

    expect(isDark()).toBe(true);
    expect(useThemeStore.getState().resolved).toBe('dark');
  });

  it('removes the dark class when switching back to light', () => {
    useThemeStore.getState().setPreference('dark');
    useThemeStore.getState().setPreference('light');

    expect(isDark()).toBe(false);
    expect(useThemeStore.getState().resolved).toBe('light');
  });

  it('resolves "system" against the OS setting', () => {
    stubSystemPreference(true);
    useThemeStore.getState().setPreference('system');

    expect(useThemeStore.getState().resolved).toBe('dark');
    expect(isDark()).toBe(true);
  });
});

describe('toggle', () => {
  it('flips light to dark and back', () => {
    useThemeStore.getState().setPreference('light');

    useThemeStore.getState().toggle();
    expect(useThemeStore.getState().preference).toBe('dark');

    useThemeStore.getState().toggle();
    expect(useThemeStore.getState().preference).toBe('light');
  });

  it('resolves "system" to an explicit choice rather than staying on system', () => {
    stubSystemPreference(false);
    useThemeStore.getState().setPreference('system');

    useThemeStore.getState().toggle();

    expect(useThemeStore.getState().preference).toBe('dark');
  });
});

describe('syncWithSystem', () => {
  it('follows the OS while the preference is "system"', () => {
    stubSystemPreference(false);
    useThemeStore.getState().setPreference('system');

    stubSystemPreference(true);
    useThemeStore.getState().syncWithSystem();

    expect(useThemeStore.getState().resolved).toBe('dark');
  });

  it('ignores the OS once the user has chosen explicitly', () => {
    useThemeStore.getState().setPreference('light');

    stubSystemPreference(true);
    useThemeStore.getState().syncWithSystem();

    expect(useThemeStore.getState().resolved).toBe('light');
    expect(isDark()).toBe(false);
  });
});

describe('initTheme', () => {
  it('applies the stored preference immediately', () => {
    useThemeStore.setState({ preference: 'dark', resolved: 'dark' });
    const dispose = initTheme();

    expect(isDark()).toBe(true);
    dispose();
  });

  it('re-resolves when the OS preference changes', () => {
    const listeners: { change?: () => void } = {};
    stubSystemPreference(false, listeners);
    useThemeStore.getState().setPreference('system');

    const dispose = initTheme();
    stubSystemPreference(true, listeners);
    listeners.change?.();

    expect(useThemeStore.getState().resolved).toBe('dark');
    dispose();
  });

  it('returns a disposer even where matchMedia is unavailable', () => {
    vi.stubGlobal('matchMedia', undefined);
    expect(() => initTheme()()).not.toThrow();
  });
});
