import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios';
import { describe, expect, it } from 'vitest';
import { ApiError, normalizeError, type ProblemDetails } from './http-error';

function axiosErrorWithStatus(status: number, data: unknown = {}): AxiosError {
  const config = { headers: new AxiosHeaders() };
  const response = { status, data, statusText: '', headers: {}, config } as AxiosResponse;
  return new AxiosError('Request failed', 'ERR_BAD_RESPONSE', config, {}, response);
}

describe('normalizeError — transport failures', () => {
  it('maps a response-less error to network', () => {
    const error = normalizeError(new AxiosError('Network Error', 'ERR_NETWORK'));
    expect(error.kind).toBe('network');
    expect(error.message).toMatch(/could not reach the server/i);
  });

  it('maps ECONNABORTED to timeout', () => {
    const error = normalizeError(new AxiosError('timeout', 'ECONNABORTED'));
    expect(error.kind).toBe('timeout');
    expect(error.message).toMatch(/too long/i);
  });
});

describe('normalizeError — status mapping', () => {
  it.each([
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [404, 'notFound'],
    [409, 'conflict'],
    [422, 'validation'],
    [429, 'rateLimited'],
    [500, 'server'],
    [503, 'server'],
    [400, 'client'],
  ])('maps %i to %s', (status, kind) => {
    expect(normalizeError(axiosErrorWithStatus(status)).kind).toBe(kind);
  });

  it('flags 401 via isUnauthorized', () => {
    expect(normalizeError(axiosErrorWithStatus(401)).isUnauthorized).toBe(true);
  });

  it('marks only transient failures retryable', () => {
    expect(normalizeError(axiosErrorWithStatus(500)).isRetryable).toBe(true);
    expect(normalizeError(new AxiosError('x', 'ERR_NETWORK')).isRetryable).toBe(true);
    expect(normalizeError(axiosErrorWithStatus(404)).isRetryable).toBe(false);
    expect(normalizeError(axiosErrorWithStatus(422)).isRetryable).toBe(false);
  });
});

describe('normalizeError — RFC 9457 problem documents', () => {
  const problem: ProblemDetails = {
    type: 'urn:schoolmanagement:error:student.not_enrolled',
    title: 'Student not enrolled',
    status: 409,
    detail: 'This student is not enrolled for the selected term.',
    errorCode: 'student.not_enrolled',
    traceId: '0af7651916cd43dd8448eb211c80319c',
  };

  it('prefers the server detail as the user-facing message', () => {
    expect(normalizeError(axiosErrorWithStatus(409, problem)).message).toBe(problem.detail);
  });

  it('exposes the problem document errorCode as the stable machine-readable code', () => {
    expect(normalizeError(axiosErrorWithStatus(409, problem)).errorCode).toBe(problem.errorCode);
  });

  it('exposes the problem document traceId for support correlation', () => {
    expect(normalizeError(axiosErrorWithStatus(409, problem)).traceId).toBe(problem.traceId);
  });

  it('falls back to the title when there is no detail', () => {
    const { detail: _detail, ...withoutDetail } = problem;
    expect(normalizeError(axiosErrorWithStatus(409, withoutDetail)).message).toBe(problem.title);
  });

  it('falls back to a generic message when the body is not a problem document', () => {
    const error = normalizeError(axiosErrorWithStatus(409, 'plain text'));
    expect(error.message).toMatch(/conflicts with the current state/i);
  });

  it('leaves errorCode undefined for a problem response the framework produced directly', () => {
    // e.g. a model-binding 400 or a middleware 401 — no errorCode, per the contract.
    const { errorCode: _errorCode, ...withoutErrorCode } = problem;
    const error = normalizeError(axiosErrorWithStatus(401, withoutErrorCode));
    expect(error.errorCode).toBeUndefined();
    // The fallback path a caller uses instead: branch on `kind`, not `errorCode`.
    expect(error.isUnauthorized).toBe(true);
  });

  it('surfaces field errors for a validation failure', () => {
    const error = normalizeError(
      axiosErrorWithStatus(422, {
        traceId: problem.traceId,
        errors: { admissionNumber: ['Already in use.'] },
      }),
    );
    expect(error.fieldErrors).toEqual({ admissionNumber: ['Already in use.'] });
  });
});

describe('normalizeError — non-Axios input', () => {
  it('passes an existing ApiError straight through', () => {
    const original = new ApiError('already normalised', { kind: 'conflict' });
    expect(normalizeError(original)).toBe(original);
  });

  it('wraps a plain Error and keeps its message', () => {
    const error = normalizeError(new Error('boom'));
    expect(error.kind).toBe('unknown');
    expect(error.message).toBe('boom');
  });

  it('handles a thrown non-Error value', () => {
    const error = normalizeError('just a string');
    expect(error).toBeInstanceOf(ApiError);
    expect(error.kind).toBe('unknown');
  });

  it('is always an Error subclass so it survives a throw', () => {
    expect(normalizeError('x')).toBeInstanceOf(Error);
    expect(normalizeError('x').name).toBe('ApiError');
  });
});
