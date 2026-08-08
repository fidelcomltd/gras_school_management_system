import { http, HttpResponse } from 'msw';
import { API_BASE_URL } from '@/config/env-values';

/**
 * Default MSW handlers.
 *
 * Empty by design — there are no endpoints in `contracts/openapi.json` yet.
 * Once the contract exists, derive handlers from it rather than hand-writing
 * them, so mocks cannot drift from the real shapes.
 *
 * Per-test overrides go through `server.use(...)`, not this array.
 */
export const handlers = [];

/** Builds an absolute URL against the configured API base, for use in handlers. */
export const apiUrl = (path: string): string =>
  `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;

/** RFC 9457 problem document, matching what the backend returns on error. */
export function problemResponse(
  status: number,
  body: { type?: string; title?: string; detail?: string; errors?: Record<string, string[]> } = {},
) {
  return HttpResponse.json(
    { status, title: body.title ?? 'Error', ...body },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

export { http, HttpResponse };
