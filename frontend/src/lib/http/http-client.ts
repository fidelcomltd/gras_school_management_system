import axios, {
  type AxiosInstance,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';
import { API_BASE_URL, API_TIMEOUT_MS } from '@/config/env-values';
import { clearSession, getAccessToken, refreshSession } from '@/lib/auth/auth-session';

/**
 * The one Axios instance. Created once, exported once.
 *
 * Nothing outside `src/lib/http/` may import Axios or call this directly —
 * feature code goes through the verb helpers in `request.ts`, which are the
 * only functions that unwrap responses and normalise errors.
 */

type RetryableConfig = InternalAxiosRequestConfig & { authRetried?: boolean };

function newCorrelationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `cid-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

export function createHttpClient(): AxiosInstance {
  const client = axios.create({
    baseURL: API_BASE_URL,
    timeout: API_TIMEOUT_MS,
    headers: { Accept: 'application/json' },
  });

  attachAuthInterceptors(client);
  return client;
}

export function attachAuthInterceptors(client: AxiosInstance): void {
  client.interceptors.request.use((config) => {
    const token = getAccessToken();
    if (token) config.headers.set('Authorization', `Bearer ${token}`);

    // Lets a backend log line be tied to the exact UI action that caused it.
    if (!config.headers.has('X-Correlation-Id')) {
      config.headers.set('X-Correlation-Id', newCorrelationId());
    }
    return config;
  });

  client.interceptors.response.use(
    (response: AxiosResponse) => response,
    async (error: unknown) => {
      if (!axios.isAxiosError(error) || error.response?.status !== 401) {
        throw error;
      }

      const config = error.config as RetryableConfig | undefined;

      // Retry a 401 exactly once, behind the single-flight refresh. Without the
      // flag a permanently-401 endpoint would loop forever.
      if (!config || config.authRetried) {
        clearSession();
        throw error;
      }

      const renewed = await refreshSession();
      if (!renewed) {
        clearSession();
        throw error;
      }

      config.authRetried = true;
      return client.request(config);
    },
  );
}

export const httpClient = createHttpClient();
