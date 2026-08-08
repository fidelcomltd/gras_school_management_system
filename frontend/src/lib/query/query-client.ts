import { QueryClient } from '@tanstack/react-query';
import { ApiError } from '@/lib/http';

const DEFAULT_STALE_TIME_MS = 30_000;
const MAX_QUERY_ATTEMPTS = 3;

/**
 * Query defaults for the whole app.
 *
 * `staleTime` is set explicitly rather than left at 0 — the default makes every
 * mount refetch, which reads as flicker and hammers the API. Features that need
 * fresher or more durable data override it on the individual hook.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: DEFAULT_STALE_TIME_MS,
        gcTime: 5 * 60_000,
        refetchOnWindowFocus: false,
        // Retrying a 404 or a validation failure just delays the error. Only
        // network, timeout, and 5xx are worth a second attempt.
        retry: (failureCount, error) => {
          if (failureCount >= MAX_QUERY_ATTEMPTS - 1) return false;
          if (error instanceof ApiError) return error.isRetryable;
          return false;
        },
      },
      mutations: {
        // A blind mutation retry can double-submit. Endpoints that are safe to
        // retry accept an Idempotency-Key and can opt in per hook.
        retry: false,
      },
    },
  });
}
