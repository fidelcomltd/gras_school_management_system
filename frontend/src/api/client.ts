import { deleteRequest, getRequest, patchRequest, postRequest, putRequest, type RequestOptions } from '@/lib/http';
import type {
  CallerOptions,
  OperationOf,
  OptionsArgs,
  PathsWithMethod,
  QueryOf,
  RequestBodyOf,
  SuccessBody,
} from './client-types';

/**
 * Thin typed layer over `@/lib/http`, keyed by the generated OpenAPI schema.
 *
 * This file adds no HTTP behaviour of its own — no new base URL, no new error
 * handling, no bypass of the axios transport's interceptors or `ApiError`
 * normalisation. It only narrows `getRequest`/`postRequest`/`patchRequest`/
 * `deleteRequest`'s generic `<TResponse>` down to what `contracts/openapi.json`
 * actually promises for a given path, so a caller gets compile-time inference
 * instead of a hand-written interface that can silently drift from the
 * contract. The type-level derivation itself (`SuccessBody`, `QueryOf`,
 * `OptionsArgs`, …) lives in `client-types.ts`, split out to stay under
 * CONVENTIONS.md §3's 180-line cap.
 *
 * Hand-written on purpose — see `src/api/README.md`. Only `schema.d.ts` is
 * generated.
 */

/** Paths that declare a GET operation in the contract. */
export type GetPath = PathsWithMethod<'get'>;
/** Paths that declare a POST operation in the contract. */
export type PostPath = PathsWithMethod<'post'>;
/** Paths that declare a PUT operation in the contract. */
export type PutPath = PathsWithMethod<'put'>;
/** Paths that declare a PATCH operation in the contract. */
export type PatchPath = PathsWithMethod<'patch'>;
/** Paths that declare a DELETE operation in the contract. */
export type DeletePath = PathsWithMethod<'delete'>;

/**
 * Substitutes `{name}` placeholders in a contract path with the caller's
 * path parameter values. Every value the contract declares is required by
 * the route itself, so a missing one throws rather than sending a request
 * against a literal `{id}` segment.
 */
function buildPath(path: string, pathParams: unknown): string {
  if (pathParams === undefined) return path;
  const values = pathParams as Record<string, string | number>;
  return path.replace(/\{([^}]+)\}/g, (_match, name: string) => {
    const value = values[name];
    if (value === undefined) {
      throw new Error(`apiClient: missing path parameter "${name}" for "${path}"`);
    }
    return encodeURIComponent(String(value));
  });
}

/**
 * Pulls the operation-specific `pathParams`/`idempotencyKey` fields back out
 * of a caller's options bag, leaving plain `RequestOptions` for the
 * transport. `idempotencyKey` is the only header this layer ever sets by
 * hand — cast to a loose record here because the precise per-operation shape
 * was already enforced at the call site by `OptionsArgs`; nothing downstream
 * of this function sees anything but `RequestOptions`.
 */
function splitOptions(options: object | undefined): { pathParams: unknown; request: RequestOptions } {
  if (!options) return { pathParams: undefined, request: {} };
  const { pathParams, idempotencyKey, ...rest } = options as {
    pathParams?: unknown;
    idempotencyKey?: string;
  } & Record<string, unknown>;
  const request = rest as RequestOptions;
  if (typeof idempotencyKey === 'string') {
    request.headers = { 'Idempotency-Key': idempotencyKey };
  }
  return { pathParams, request };
}

/**
 * GETs a path declared in `contracts/openapi.json`. Query parameters, path
 * parameters and the response body are all inferred from the schema — never
 * asserted.
 */
export function apiGet<P extends GetPath>(
  path: P,
  params: QueryOf<OperationOf<P, 'get'>>,
  ...rest: OptionsArgs<OperationOf<P, 'get'>, Omit<CallerOptions, 'params'>>
): Promise<SuccessBody<OperationOf<P, 'get'>>> {
  const { pathParams, request } = splitOptions(rest[0]);
  const config: RequestOptions = {
    ...request,
    // `params` is a generic type parameter here, so TS cannot narrow it past
    // `undefined` on its own; the schema guarantees this is always the
    // operation's own query-object shape, which is what `RequestOptions`
    // expects.
    ...(params === undefined ? {} : { params: params as NonNullable<RequestOptions['params']> }),
  };
  return getRequest<SuccessBody<OperationOf<P, 'get'>>>(buildPath(path, pathParams), config);
}

/**
 * POSTs a path declared in `contracts/openapi.json`. The request body, path
 * parameters and the response body are all inferred from the schema;
 * `Idempotency-Key` is required or optional exactly as the contract says.
 */
export function apiPost<P extends PostPath>(
  path: P,
  body: RequestBodyOf<OperationOf<P, 'post'>>,
  ...rest: OptionsArgs<OperationOf<P, 'post'>, CallerOptions>
): Promise<SuccessBody<OperationOf<P, 'post'>>> {
  const { pathParams, request } = splitOptions(rest[0]);
  return postRequest<SuccessBody<OperationOf<P, 'post'>>, RequestBodyOf<OperationOf<P, 'post'>>>(
    buildPath(path, pathParams),
    body,
    request,
  );
}

/**
 * PUTs a path declared in `contracts/openapi.json` — the transport
 * (`putRequest`) already existed in `@/lib/http`; this is the typed wrapper
 * the first PUT caller (`/arms/{armId}/score-sheets`, TASK-0079) needs and
 * previously had no way to reach without bypassing `src/api/`. Mirrors
 * `apiPatch` exactly: same CSRF handling (transport-owned, never a field
 * here), same error normalisation, same `SuccessBody`/`RequestBodyOf`
 * derivation — only the verb differs.
 */
export function apiPut<P extends PutPath>(
  path: P,
  body: RequestBodyOf<OperationOf<P, 'put'>>,
  ...rest: OptionsArgs<OperationOf<P, 'put'>, CallerOptions>
): Promise<SuccessBody<OperationOf<P, 'put'>>> {
  const { pathParams, request } = splitOptions(rest[0]);
  return putRequest<SuccessBody<OperationOf<P, 'put'>>, RequestBodyOf<OperationOf<P, 'put'>>>(
    buildPath(path, pathParams),
    body,
    request,
  );
}

/**
 * PATCHes a path declared in `contracts/openapi.json` — the transport
 * (`patchRequest`) already existed in `@/lib/http`; this is the typed
 * wrapper the first PATCH callers (`/admins/{id}`, `/settings/identity`)
 * need and previously had no way to reach without bypassing `src/api/`.
 */
export function apiPatch<P extends PatchPath>(
  path: P,
  body: RequestBodyOf<OperationOf<P, 'patch'>>,
  ...rest: OptionsArgs<OperationOf<P, 'patch'>, CallerOptions>
): Promise<SuccessBody<OperationOf<P, 'patch'>>> {
  const { pathParams, request } = splitOptions(rest[0]);
  return patchRequest<SuccessBody<OperationOf<P, 'patch'>>, RequestBodyOf<OperationOf<P, 'patch'>>>(
    buildPath(path, pathParams),
    body,
    request,
  );
}

/**
 * DELETEs a path declared in `contracts/openapi.json`. `deleteRequest`'s
 * default `TResponse = void` lines up with `SuccessBody` resolving `void`
 * for this contract's one `DELETE` operation (`204 No Content`), so a
 * caller awaiting the result gets `Promise<void>` rather than `never`.
 */
export function apiDelete<P extends DeletePath>(
  path: P,
  ...rest: OptionsArgs<OperationOf<P, 'delete'>, CallerOptions>
): Promise<SuccessBody<OperationOf<P, 'delete'>>> {
  const { pathParams, request } = splitOptions(rest[0]);
  return deleteRequest<SuccessBody<OperationOf<P, 'delete'>>>(buildPath(path, pathParams), request);
}
