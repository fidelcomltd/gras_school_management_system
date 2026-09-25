import axios from 'axios';
import type { components } from '@/api/schema';

/**
 * RFC 9457 problem document, straight from the generated contract types — not
 * hand-duplicated. Covers every error response; `HttpValidationProblemDetails`
 * additionally carries field-keyed `errors` for a 422.
 *
 * `errorCode` is declared but NOT required by the contract: a problem response
 * the framework produces directly (model binding, auth middleware) never passes
 * through this API's own result mapping, so it genuinely carries no `errorCode`.
 * Callers must presence-check it, never assert it non-null.
 */
export type ProblemDetails = components['schemas']['ProblemDetails'];
export type ValidationProblemDetails = components['schemas']['HttpValidationProblemDetails'];
type AnyProblemDetails = ProblemDetails | ValidationProblemDetails;

function fieldErrorsOf(problem: AnyProblemDetails): Record<string, string[]> | undefined {
  return 'errors' in problem ? problem.errors : undefined;
}

export type ApiErrorKind =
  | 'network'
  | 'timeout'
  | 'canceled'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'validation'
  | 'rateLimited'
  | 'server'
  | 'client'
  | 'unknown';

const KIND_BY_STATUS: ReadonlyMap<number, ApiErrorKind> = new Map([
  [401, 'unauthorized'],
  [403, 'forbidden'],
  [404, 'notFound'],
  [409, 'conflict'],
  [422, 'validation'],
  [429, 'rateLimited'],
]);

const MESSAGE_BY_KIND: Readonly<Record<ApiErrorKind, string>> = {
  network: 'Could not reach the server. Check your connection and try again.',
  timeout: 'The server took too long to respond. Please try again.',
  canceled: 'The request was cancelled.',
  unauthorized: 'Your session has ended. Please sign in again.',
  forbidden: 'You do not have permission to do that.',
  notFound: 'We could not find what you were looking for.',
  conflict: 'That change conflicts with the current state. Refresh and try again.',
  validation: 'Some of the information provided is not valid.',
  rateLimited: 'Too many requests. Please wait a moment and try again.',
  server: 'Something went wrong on our end. Please try again shortly.',
  client: 'The request could not be completed.',
  unknown: 'An unexpected error occurred.',
};

function kindForStatus(status: number): ApiErrorKind {
  const mapped = KIND_BY_STATUS.get(status);
  if (mapped) return mapped;
  if (status >= 500) return 'server';
  if (status >= 400) return 'client';
  return 'unknown';
}

function isProblemDetails(body: unknown): body is AnyProblemDetails {
  return typeof body === 'object' && body !== null && !Array.isArray(body);
}

/** The one 401 that leaves the session alive; see `ApiError.endsSession`. */
export const CURRENT_PASSWORD_INCORRECT = 'auth.current_password_incorrect';

/**
 * A single error type for every failure crossing the HTTP boundary.
 *
 * `message` is always safe to render to a user. Raw Axios errors never escape
 * the http layer — callers and UI only ever see this.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | undefined;
  /**
   * The problem document's stable, machine-readable `errorCode` — branch on
   * this, never on `message`/`detail`. Genuinely absent when the response was
   * produced directly by the framework rather than this API's own result
   * mapping (a model-binding 400, a 401 from auth middleware): callers must
   * presence-check it and fall back to `kind` for those, not assume it is set.
   */
  readonly errorCode: string | undefined;
  /** Correlation id for this response occurrence; quote it when reporting a problem. */
  readonly traceId: string | undefined;
  readonly fieldErrors: Readonly<Record<string, string[]>> | undefined;
  readonly problem: AnyProblemDetails | undefined;

  constructor(
    message: string,
    init: {
      kind: ApiErrorKind;
      status?: number | undefined;
      errorCode?: string | undefined;
      traceId?: string | undefined;
      fieldErrors?: Record<string, string[]> | undefined;
      problem?: AnyProblemDetails | undefined;
      cause?: unknown;
    },
  ) {
    super(message, init.cause === undefined ? undefined : { cause: init.cause });
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.errorCode = init.errorCode;
    this.traceId = init.traceId;
    this.fieldErrors = init.fieldErrors;
    this.problem = init.problem;
  }

  get isUnauthorized(): boolean {
    return this.kind === 'unauthorized';
  }

  /**
   * Whether this failure means the session is over (delta §3a: every 401 is terminal) — except a wrong CURRENT
   * password on `POST /auth/password`, a failed re-authentication inside a live session that the form shows on the field.
   */
  get endsSession(): boolean {
    return this.isUnauthorized && this.errorCode !== CURRENT_PASSWORD_INCORRECT;
  }

  /** Transient failures — safe for a retry policy to act on. */
  get isRetryable(): boolean {
    return this.kind === 'network' || this.kind === 'timeout' || this.kind === 'server';
  }
}

/** Converts anything thrown by Axios (or by us) into an `ApiError`. */
export function normalizeError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;

  if (axios.isCancel(error)) {
    return new ApiError(MESSAGE_BY_KIND.canceled, { kind: 'canceled', cause: error });
  }

  if (axios.isAxiosError(error)) {
    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
      return new ApiError(MESSAGE_BY_KIND.timeout, { kind: 'timeout', cause: error });
    }

    if (!error.response) {
      return new ApiError(MESSAGE_BY_KIND.network, { kind: 'network', cause: error });
    }

    const { status, data } = error.response;
    const kind = kindForStatus(status);
    const problem = isProblemDetails(data) ? data : undefined;

    // Prefer the server's own wording, but never surface a bare status line.
    const message = problem?.detail?.trim() || problem?.title?.trim() || MESSAGE_BY_KIND[kind];

    // `errorCode` is a presence check, not an assertion — the contract deliberately
    // leaves it unset for framework-produced problem responses (see `ApiError.errorCode`).
    return new ApiError(message, {
      kind,
      status,
      errorCode: problem?.errorCode,
      traceId: problem?.traceId,
      fieldErrors: problem && fieldErrorsOf(problem),
      problem,
      cause: error,
    });
  }

  if (error instanceof Error) {
    return new ApiError(error.message || MESSAGE_BY_KIND.unknown, {
      kind: 'unknown',
      cause: error,
    });
  }

  return new ApiError(MESSAGE_BY_KIND.unknown, { kind: 'unknown', cause: error });
}
