import { http, HttpResponse } from 'msw';
import { API_BASE_URL } from '@/config/env-values';
import { buildHandlersFromContract } from './openapi-handlers';

/**
 * Default MSW handlers, derived from `contracts/openapi.json` (see
 * `./openapi-handlers.ts`) rather than hand-written, so mocks cannot drift
 * from the contract. Each covers the happy path for one operation.
 *
 * Per-test overrides go through `server.use(...)`, not this array — that is
 * how error scenarios (`422`, `401`, `409`, …) get exercised.
 */
export const handlers = buildHandlersFromContract(API_BASE_URL);

/** Builds an absolute URL against the configured API base, for use in handlers. */
export const apiUrl = (path: string): string =>
  `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;

/** RFC 9457 problem document, matching what the backend returns on error. */
export function problemResponse(
  status: number,
  body: {
    type?: string;
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
    errorCode?: string;
    traceId?: string;
  } = {},
) {
  return HttpResponse.json(
    { status, title: body.title ?? 'Error', ...body },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

export { http, HttpResponse };
