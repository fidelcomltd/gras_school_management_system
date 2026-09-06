import type { components } from '@/api/schema';

/**
 * Small, dependency-free predicates used by `http-client.ts`'s interceptors.
 * Split out only to keep that file under the 180-line convention cap — these
 * are not a reusable public surface for feature code.
 */

type AuthSessionResponseDto = components['schemas']['AuthSessionResponse'];

/** Narrows an unknown response body to the shape `setSession` needs, without a cast. */
export function isAuthSessionResponse(data: unknown): data is AuthSessionResponseDto {
  return (
    typeof data === 'object' &&
    data !== null &&
    'sessionExpiresAt' in data &&
    'sessionAbsoluteExpiresAt' in data &&
    'mustChangePassword' in data
  );
}

/** Reads a problem document's `errorCode` without assuming the body is one. */
export function errorCodeOf(data: unknown): string | undefined {
  if (typeof data !== 'object' || data === null) return undefined;
  const errorCode = (data as Record<string, unknown>)['errorCode'];
  return typeof errorCode === 'string' ? errorCode : undefined;
}

export function newCorrelationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `cid-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}
