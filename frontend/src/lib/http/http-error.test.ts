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
    type: 'https://errors.gra.school/student-not-enrolled',
    title: 'Student not enrolled',
    status: 409,
    detail: 'This student is not enrolled for the selected term.',
  };

  it('prefers the server detail as the user-facing message', () => {
    expect(normalizeError(axiosErrorWithStatus(409, problem)).message).toBe(problem.detail);
  });

  it('exposes the problem type as a stable machine-readable code', () => {
    expect(normalizeError(axiosErrorWithStatus(409, problem)).code).toBe(problem.type);
  });

  it('falls back to the title when there is no detail', () => {
    const { detail: _detail, ...withoutDetail } = problem;
    expect(normalizeError(axiosErrorWithStatus(409, withoutDetail)).message).toBe(problem.title);
  });

  it('falls back to a generic message when the body is not a problem document', () => {
    const error = normalizeError(axiosErrorWithStatus(409, 'plain text'));
    expect(error.message).toMatch(/conflicts with the current state/i);
  });

  it('surfaces field errors for a validation failure', () => {
    const error = normalizeError(
      axiosErrorWithStatus(422, { errors: { admissionNumber: ['Already in use.'] } }),
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
