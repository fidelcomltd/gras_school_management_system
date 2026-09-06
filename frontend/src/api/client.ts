import { getRequest, postRequest, type RequestOptions } from '@/lib/http';
import type { paths } from './schema';

/**
 * Thin typed layer over `@/lib/http`, keyed by the generated OpenAPI schema.
 *
 * This file adds no HTTP behaviour of its own — no new base URL, no new error
 * handling, no bypass of the axios transport's interceptors or `ApiError`
 * normalisation. It only narrows `getRequest`/`postRequest`'s generic
 * `<TResponse>` down to what `contracts/openapi.json` actually promises for a
 * given path, so a caller gets compile-time inference instead of a
 * hand-written interface that can silently drift from the contract.
 *
 * Hand-written on purpose — see `src/api/README.md`. Only `schema.d.ts` is
 * generated.
 */

type Method = 'get' | 'post' | 'put' | 'patch' | 'delete';

type PathsWithMethod<M extends Method> = {
  [P in keyof paths]: paths[P] extends Record<M, object> ? P : never;
}[keyof paths];

type OperationOf<P extends keyof paths, M extends Method> = paths[P] extends Record<M, infer Op>
  ? Op
  : never;

type JsonOf<Content> = Content extends { 'application/json': infer J } ? J : never;

/** The first successful (2xx) JSON response body an operation declares. */
type SuccessBody<Op> = Op extends { responses: infer R }
  ? R extends { 200: { content: infer C } }
    ? JsonOf<C>
    : R extends { 201: { content: infer C } }
      ? JsonOf<C>
      : never
  : never;

/**
 * Normalises `never` to `undefined`. `openapi-typescript` emits a literal
 * `query?: never` / `requestBody?: never` for an operation that declares
 * none — distinct from the property being absent — so a plain conditional
 * match on that position infers `never` itself rather than "nothing to pass".
 * Tuple-wrapped so this checks for `never` exactly, without a union
 * distributing across the conditional.
 */
type NeverToUndefined<T> = [T] extends [never] ? undefined : T;

/**
 * An operation's query parameters, or `undefined` when it declares none.
 * Matched against an optional `query?:` position so both a required query
 * object (e.g. `ping`) and an optional one (e.g. `records`) resolve to the
 * object's own shape rather than falling through to `undefined`.
 */
type QueryOf<Op> = Op extends { parameters: { query?: infer Q } } ? NeverToUndefined<Q> : undefined;

/** An operation's JSON request body, or `undefined` when it declares none. */
type RequestBodyOf<Op> = Op extends { requestBody?: { content: infer C } }
  ? NeverToUndefined<JsonOf<C>>
  : undefined;

/** Paths that declare a GET operation in the contract. */
export type GetPath = PathsWithMethod<'get'>;
/** Paths that declare a POST operation in the contract. */
export type PostPath = PathsWithMethod<'post'>;

/**
 * GETs a path declared in `contracts/openapi.json`. Query parameters and the
 * response body are both inferred from the schema — never asserted.
 */
export function apiGet<P extends GetPath>(
  path: P,
  params: QueryOf<OperationOf<P, 'get'>>,
  options?: Omit<RequestOptions, 'params'>,
): Promise<SuccessBody<OperationOf<P, 'get'>>> {
  const config: RequestOptions = {
    ...options,
    // `params` is a generic type parameter here, so TS cannot narrow it past
    // `undefined` on its own; the schema guarantees this is always the
    // operation's own query-object shape, which is what `RequestOptions`
    // expects.
    ...(params === undefined ? {} : { params: params as NonNullable<RequestOptions['params']> }),
  };
  return getRequest<SuccessBody<OperationOf<P, 'get'>>>(path, config);
}

/**
 * POSTs a path declared in `contracts/openapi.json`. The request body and
 * the response body are both inferred from the schema.
 */
export function apiPost<P extends PostPath>(
  path: P,
  body: RequestBodyOf<OperationOf<P, 'post'>>,
  options?: RequestOptions,
): Promise<SuccessBody<OperationOf<P, 'post'>>> {
  return postRequest<SuccessBody<OperationOf<P, 'post'>>, RequestBodyOf<OperationOf<P, 'post'>>>(
    path,
    body,
    options,
  );
}
