import { describe, expect, it, vi } from 'vitest';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { ApiError } from './http-error';
import { deleteRequest, getRequest, patchRequest, postRequest, putRequest } from './request';

/**
 * Auth wiring — CSRF attachment, the terminal-401 path, the single CSRF retry —
 * moved to `./http-client.test.ts` (TASK-0021): those behaviours belong to
 * `http-client.ts`'s interceptors, which is what that file exercises directly.
 */

interface Term {
  id: string;
  name: string;
}

const TERM: Term = { id: 't1', name: 'Michaelmas' };

describe('payload unwrapping', () => {
  it('returns the response body, not the Axios envelope', async () => {
    server.use(http.get(apiUrl('/terms/t1'), () => HttpResponse.json(TERM)));

    const result = await getRequest<Term>('/terms/t1');

    expect(result).toEqual(TERM);
    // A leaked envelope would show up as these keys.
    expect(result).not.toHaveProperty('data');
    expect(result).not.toHaveProperty('status');
  });

  it('returns undefined for 204 No Content rather than an empty string', async () => {
    server.use(http.delete(apiUrl('/terms/t1'), () => new HttpResponse(null, { status: 204 })));
    await expect(deleteRequest('/terms/t1')).resolves.toBeUndefined();
  });

  it('sends query parameters', async () => {
    const seen = vi.fn();
    server.use(
      http.get(apiUrl('/terms'), ({ request }) => {
        seen(new URL(request.url).searchParams.get('page'));
        return HttpResponse.json([TERM]);
      }),
    );

    await getRequest<Term[]>('/terms', { params: { page: 2 } });
    expect(seen).toHaveBeenCalledWith('2');
  });
});

describe('verbs', () => {
  it('posts a body and returns the created resource', async () => {
    server.use(
      http.post(apiUrl('/terms'), async ({ request }) =>
        HttpResponse.json(await request.json(), { status: 201 }),
      ),
    );

    await expect(postRequest<Term, { name: string }>('/terms', { name: 'Hilary' })).resolves.toEqual(
      { name: 'Hilary' },
    );
  });

  it('puts a body', async () => {
    server.use(http.put(apiUrl('/terms/t1'), () => new HttpResponse(null, { status: 204 })));
    await expect(putRequest<void, Term>('/terms/t1', TERM)).resolves.toBeUndefined();
  });

  it('patches a body', async () => {
    server.use(http.patch(apiUrl('/terms/t1'), () => HttpResponse.json(TERM)));
    await expect(patchRequest<Term, { name: string }>('/terms/t1', { name: 'x' })).resolves.toEqual(
      TERM,
    );
  });
});

describe('error handling', () => {
  it('throws an ApiError carrying the server-supplied message', async () => {
    server.use(
      http.get(apiUrl('/terms/missing'), () =>
        problemResponse(404, {
          type: 'https://errors.gra.school/term-not-found',
          detail: 'No term with that identifier.',
          errorCode: 'term.not_found',
        }),
      ),
    );

    const error = await getRequest('/terms/missing').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe('No term with that identifier.');
    expect((error as ApiError).kind).toBe('notFound');
    expect((error as ApiError).errorCode).toBe('term.not_found');
  });

  it('never lets a raw Axios error escape', async () => {
    server.use(http.get(apiUrl('/boom'), () => new HttpResponse(null, { status: 500 })));

    const error = await getRequest('/boom').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).not.toHaveProperty('isAxiosError');
  });

  it('exposes field errors from a 422', async () => {
    server.use(
      http.post(apiUrl('/terms'), () =>
        problemResponse(422, { errors: { name: ['Name is required.'] } }),
      ),
    );

    const error = (await postRequest('/terms', {}).catch((caught: unknown) => caught)) as ApiError;
    expect(error.kind).toBe('validation');
    expect(error.fieldErrors).toEqual({ name: ['Name is required.'] });
  });
});

describe('correlation id', () => {
  it('always sends one', async () => {
    const seen = vi.fn();
    server.use(
      http.get(apiUrl('/terms'), ({ request }) => {
        seen(request.headers.get('X-Correlation-Id'));
        return HttpResponse.json([]);
      }),
    );

    await getRequest('/terms');
    expect(seen).toHaveBeenCalledWith(expect.stringMatching(/.+/));
  });
});
