import axios from 'axios';

/**
 * RFC 9457 problem document. The backend spec commits to returning this shape
 * for every error, with a stable machine-readable `type`.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Field-keyed validation messages, as emitted by the validation filter. */
  errors?: Record<string, string[]>;
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

function isProblemDetails(body: unknown): body is ProblemDetails {
  return typeof body === 'object' && body !== null && !Array.isArray(body);
}

/**
 * A single error type for every failure crossing the HTTP boundary.
 *
 * `message` is always safe to render to a user. Raw Axios errors never escape
 * the http layer — callers and UI only ever see this.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | undefined;
  /** Stable machine-readable code from the problem document's `type`, when present. */
  readonly code: string | undefined;
  readonly fieldErrors: Readonly<Record<string, string[]>> | undefined;
  readonly problem: ProblemDetails | undefined;

  constructor(
    message: string,
    init: {
      kind: ApiErrorKind;
      status?: number | undefined;
      code?: string | undefined;
      fieldErrors?: Record<string, string[]> | undefined;
      problem?: ProblemDetails | undefined;
      cause?: unknown;
    },
  ) {
    super(message, init.cause === undefined ? undefined : { cause: init.cause });
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.code = init.code;
    this.fieldErrors = init.fieldErrors;
    this.problem = init.problem;
  }

  get isUnauthorized(): boolean {
    return this.kind === 'unauthorized';
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

    return new ApiError(message, {
      kind,
      status,
      code: problem?.type,
      fieldErrors: problem?.errors,
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
