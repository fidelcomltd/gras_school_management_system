import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { __resetAuthSession } from '@/lib/auth/auth-session';
import { server } from './msw/server';

/**
 * Global test setup. Anything here applies to every test file.
 *
 * The guiding rule: no state crosses a test boundary. Session, DOM, MSW
 * handlers, and the theme class are all reset after each case.
 */

// findBy*/waitFor default to 1 s, which a first render under full-suite load (66 files in parallel) can miss:
// `verify` alternated 419/419 and 418/419 on unchanged code (drift 2026-09-23). A longer ceiling only delays
// a genuine failure; it never lets a missing element pass.
configure({ asyncUtilTimeout: 3000 });

// jsdom has no matchMedia. The theme store calls it on load, so it is stubbed
// before any module can reach for it.
if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
}

// Base UI's popups measure and animate; jsdom implements neither.
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = vi.fn();
}
if (!window.ResizeObserver) {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

// `error` makes an unhandled request fail the test rather than hit the network.
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  cleanup();
  server.resetHandlers();
  __resetAuthSession();
  localStorage.clear();
  document.documentElement.classList.remove('dark');
});

afterAll(() => {
  server.close();
});
