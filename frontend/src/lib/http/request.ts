import type { AxiosRequestConfig, AxiosResponse } from 'axios';
import { expireSession } from '@/lib/auth/auth-session';
import { httpClient } from './http-client';
import { ApiError, normalizeError } from './http-error';

/**
 * Typed helpers, one per verb. Feature hooks call these; nothing else.
 *
 * Each helper:
 *   - returns only the server payload, never the Axios response envelope;
 *   - throws an `ApiError` whose `message` is safe to show a user;
 *   - logs the session out when the server says the credential is dead.
 */

/** The slice of Axios config a caller has any business setting. */
export interface RequestOptions {
  /** Query string values. Arrays serialise as repeated keys. */
  params?: Record<string, string | number | boolean | undefined | null | string[]>;
  /** Wire a TanStack Query `signal` in here so cancelled queries abort in flight. */
  signal?: AbortSignal;
  headers?: Record<string, string>;
  timeout?: number;
}

function toAxiosConfig(options: RequestOptions | undefined): AxiosRequestConfig {
  if (!options) return {};
  const config: AxiosRequestConfig = {};
  if (options.params !== undefined) config.params = options.params;
  if (options.signal !== undefined) config.signal = options.signal;
  if (options.headers !== undefined) config.headers = options.headers;
  if (options.timeout !== undefined) config.timeout = options.timeout;
  return config;
}

/**
 * `204 No Content` carries an empty body. Handing back `""` would make
 * `TResponse = void` callers see a string, so it is normalised to undefined.
 */
function unwrap<TResponse>(response: AxiosResponse<TResponse>): TResponse {
  if (response.status === 204 || response.data === '') {
    return undefined as TResponse;
  }
  return response.data;
}

async function send<TResponse>(
  operation: () => Promise<AxiosResponse<TResponse>>,
): Promise<TResponse> {
  try {
    return unwrap(await operation());
  } catch (error) {
    const apiError = normalizeError(error);

    // The interceptor already tried to refresh. Reaching here with a 401 means
    // the session is genuinely finished — tear it down so the UI can react.
    if (apiError.isUnauthorized) expireSession();

    throw apiError;
  }
}

export function getRequest<TResponse>(
  url: string,
  options?: RequestOptions,
): Promise<TResponse> {
  return send(() => httpClient.get<TResponse>(url, toAxiosConfig(options)));
}

export function postRequest<TResponse, TBody = undefined>(
  url: string,
  payload?: TBody,
  options?: RequestOptions,
): Promise<TResponse> {
  return send(() => httpClient.post<TResponse>(url, payload, toAxiosConfig(options)));
}

export function putRequest<TResponse, TBody = undefined>(
  url: string,
  payload?: TBody,
  options?: RequestOptions,
): Promise<TResponse> {
  return send(() => httpClient.put<TResponse>(url, payload, toAxiosConfig(options)));
}

export function patchRequest<TResponse, TBody = undefined>(
  url: string,
  payload?: TBody,
  options?: RequestOptions,
): Promise<TResponse> {
  return send(() => httpClient.patch<TResponse>(url, payload, toAxiosConfig(options)));
}

export function deleteRequest<TResponse = void>(
  url: string,
  options?: RequestOptions,
): Promise<TResponse> {
  return send(() => httpClient.delete<TResponse>(url, toAxiosConfig(options)));
}

export { ApiError };
