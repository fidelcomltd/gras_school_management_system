import axios, {
  type AxiosInstance,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';
import type { components } from '@/api/schema';
import { API_BASE_URL, API_TIMEOUT_MS } from '@/config/env-values';
import {
  getCsrfToken,
  registerKeepaliveCaller,
  setCsrfToken,
  setSession,
  terminateSession,
} from '@/lib/auth/auth-session';
import { errorCodeOf, isAuthSessionResponse, newCorrelationId } from './http-client-guards';

/**
 * The one Axios instance. Created once, exported once.
 *
 * Nothing outside `src/lib/http/` may import Axios or call this directly —
 * feature code goes through the verb helpers in `request.ts`, the only
 * functions that unwrap responses and normalise errors.
 *
 * Cookie session model (TASK-0021): CSRF attachment, the single CSRF retry, the
 * terminal 401 → sign-out path, and arming the keepalive schedule from every
 * `AuthSessionResponse` all live here so each is implemented exactly once
 * (root CLAUDE.md §5 / CONVENTIONS.md §7).
 */

type AuthSessionResponseDto = components['schemas']['AuthSessionResponse'];
type CsrfTokenResponseDto = components['schemas']['CsrfTokenResponse'];

type RetryableConfig = InternalAxiosRequestConfig & { csrfRetried?: boolean };

const CSRF_TOKEN_PATH = '/api/v1/auth/csrf';

/** Every operation returning the shared `AuthSessionResponse` shape (delta §0). */
const AUTH_SESSION_PATHS = new Set([
  '/api/v1/auth/sign-in',
  '/api/v1/auth/me',
  '/api/v1/auth/refresh',
  '/api/v1/auth/password',
]);

/** Endpoints the backend rotates the CSRF cookie against (delta §5, ruling 3). */
const CSRF_ROTATING_PATHS = new Set(['/api/v1/auth/sign-in', '/api/v1/auth/password']);

/**
 * `GET /auth/csrf` — sets the cookie server-side and returns the same value in
 * the body. Ruling 2: read the token from the BODY, never `document.cookie` —
 * the `__Host-` prefix forbids a `Domain` attribute, so the cookie is readable
 * at all only because local dev happens to share the host `localhost`; on a
 * real deployment where the API is a different host that would silently break.
 */
async function fetchCsrfToken(client: AxiosInstance): Promise<string> {
  const response = await client.get<CsrfTokenResponseDto>(CSRF_TOKEN_PATH);
  const token = response.data.csrfToken;
  setCsrfToken(token);
  return token;
}

export function createHttpClient(): AxiosInstance {
  const client = axios.create({
    baseURL: API_BASE_URL,
    timeout: API_TIMEOUT_MS,
    // Cookie session model: the browser must send/accept the session + CSRF
    // cookies on every request. Delta §6 requires this to match the backend's
    // CORS `AllowCredentials: true` + exact `AllowedOrigins` — a mismatch is a
    // §5 blocker, proved live against a running API in the task report.
    withCredentials: true,
    headers: { Accept: 'application/json' },
  });

  attachAuthInterceptors(client);
  return client;
}

export function attachAuthInterceptors(client: AxiosInstance): void {
  client.interceptors.request.use((config) => {
    const method = (config.method ?? 'get').toLowerCase();
    if (method !== 'get' && method !== 'head') {
      const token = getCsrfToken();
      if (token) config.headers.set('X-CSRF-Token', token);
    }

    // Lets a backend log line be tied to the exact UI action that caused it.
    if (!config.headers.has('X-Correlation-Id')) {
      config.headers.set('X-Correlation-Id', newCorrelationId());
    }
    return config;
  });

  client.interceptors.response.use(
    (response: AxiosResponse) => {
      const path = response.config.url;

      // Arms the keepalive schedule from EVERY AuthSessionResponse (sign-in, me,
      // refresh, password) — ruling 4 — never from a hardcoded duration.
      if (path && AUTH_SESSION_PATHS.has(path) && isAuthSessionResponse(response.data)) {
        setSession(response.data);
      }

      // CSRF rotates on sign-in and password change (delta §3). If this
      // re-fetch hasn't resolved before the next mutating request fires, that
      // request still recovers through the single CSRF retry below.
      if (path && CSRF_ROTATING_PATHS.has(path)) {
        void fetchCsrfToken(client);
      }

      return response;
    },
    async (error: unknown) => {
      if (!axios.isAxiosError(error) || !error.response) throw error;

      const status = error.response.status;

      // All three 401 variants are terminal (delta §3a): a cookie session has no
      // second credential to retry with, so a reactive 401 — from ANY endpoint,
      // including `/refresh` itself — ends the session and never calls
      // `/refresh`. `terminateSession` is idempotent/single-flight, so several
      // requests failing at once still produce exactly one sign-out transition.
      if (status === 401) {
        terminateSession();
        throw error;
      }

      // The ONLY retry in this layer (ruling 3): a 403 naming a CSRF failure
      // gets exactly one re-fetch-and-retry. Never a second, never an auth retry.
      const config = error.config as RetryableConfig | undefined;
      const errorCode = errorCodeOf(error.response.data);
      const isCsrfFailure = errorCode === 'csrf.missing' || errorCode === 'csrf.invalid';

      if (status === 403 && isCsrfFailure && config && !config.csrfRetried) {
        config.csrfRetried = true;
        const token = await fetchCsrfToken(client);
        config.headers.set('X-CSRF-Token', token);
        return client.request(config);
      }

      throw error;
    },
  );

  registerKeepaliveCaller(async () => {
    const response = await client.post<AuthSessionResponseDto>('/api/v1/auth/refresh');
    return response.data;
  });
}

export const httpClient = createHttpClient();

/**
 * Bootstraps the CSRF pair. Call once at app start (sign-in is itself
 * CSRF-protected, so something must fetch the first token) — safe to call again
 * on a hard reload while already signed in, per the endpoint's own contract.
 */
export function ensureCsrfToken(): Promise<string> {
  return fetchCsrfToken(httpClient);
}
