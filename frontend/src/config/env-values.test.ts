import { describe, expect, it } from 'vitest';
import { API_BASE_URL, APP_ENV, AUTH_EXPIRY_LEEWAY_MS, __testing } from './env-values';

const { readEnv } = __testing;

const VALID = {
  VITE_APP_NAME: 'Test Portal',
  VITE_APP_ENV: 'development',
  VITE_API_BASE_URL: 'https://api.example.com',
  VITE_API_TIMEOUT_MS: '15000',
  VITE_AUTH_EXPIRY_LEEWAY_SECONDS: '30',
  VITE_ENABLE_DEV_TOOLS: 'true',
};

const read = (overrides: Record<string, string> = {}) =>
  readEnv({ ...VALID, ...overrides } as unknown as ImportMetaEnv);

describe('env-values schema', () => {
  it('coerces numeric strings to numbers', () => {
    const env = read();
    expect(env.VITE_API_TIMEOUT_MS).toBe(15000);
    expect(typeof env.VITE_API_TIMEOUT_MS).toBe('number');
  });

  it('coerces booleanish strings to booleans', () => {
    expect(read().VITE_ENABLE_DEV_TOOLS).toBe(true);
    expect(read({ VITE_ENABLE_DEV_TOOLS: 'false' }).VITE_ENABLE_DEV_TOOLS).toBe(false);
  });

  it.each([
    ['a missing variable', { VITE_APP_NAME: '' }],
    ['a non-URL API base', { VITE_API_BASE_URL: 'not-a-url' }],
    ['an unknown environment', { VITE_APP_ENV: 'qa' }],
    ['a non-numeric timeout', { VITE_API_TIMEOUT_MS: 'soon' }],
    ['a negative leeway', { VITE_AUTH_EXPIRY_LEEWAY_SECONDS: '-1' }],
    ['a non-boolean flag', { VITE_ENABLE_DEV_TOOLS: 'yes' }],
  ])('rejects %s', (_label, overrides) => {
    expect(() => read(overrides)).toThrow(/Invalid environment configuration/);
  });

  it('names the offending variable in the error', () => {
    expect(() => read({ VITE_API_BASE_URL: 'nope' })).toThrow(/VITE_API_BASE_URL/);
  });

  it('rejects a timeout above the ceiling', () => {
    expect(() => read({ VITE_API_TIMEOUT_MS: '999999' })).toThrow();
  });
});

describe('exported constants', () => {
  it('loads under the configured test env without throwing', () => {
    // vite.config.ts's `test.env` supplies these six VITE_* variables so this
    // module loads the same way in CI (no `frontend/.env` present) as on a
    // developer machine (TASK-0023). This asserts the module-scope load
    // succeeds and exports sane constants, not that a real `.env` exists.
    expect(APP_ENV).toBeTruthy();
    expect(API_BASE_URL).toMatch(/^https?:\/\//);
  });

  it('strips any trailing slash from the API base so URL joins do not double up', () => {
    expect(API_BASE_URL.endsWith('/')).toBe(false);
  });

  it('exposes the expiry leeway in milliseconds', () => {
    expect(AUTH_EXPIRY_LEEWAY_MS % 1000).toBe(0);
    expect(AUTH_EXPIRY_LEEWAY_MS).toBeGreaterThanOrEqual(0);
  });
});
