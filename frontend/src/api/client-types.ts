import type { RequestOptions } from '@/lib/http';
import type { paths } from './schema';

/**
 * Type-level machinery behind `client.ts`'s verb helpers — kept in its own
 * file per CONVENTIONS.md §3's 180-line cap. Nothing here does anything at
 * runtime; it only derives, from the generated `paths`/`operations` types,
 * exactly what a caller must and may pass for a given contract operation.
 */

type Method = 'get' | 'post' | 'put' | 'patch' | 'delete';

export type PathsWithMethod<M extends Method> = {
  [P in keyof paths]: paths[P] extends Record<M, object> ? P : never;
}[keyof paths];

export type OperationOf<P extends keyof paths, M extends Method> = paths[P] extends Record<M, infer Op>
  ? Op
  : never;

type JsonOf<Content> = Content extends { 'application/json': infer J } ? J : never;

/**
 * A response body for whatever single content-type an operation declares.
 * `application/json` is matched first (every prior operation in this
 * contract), falling through to the declared type of any other content-type
 * key for the rest — `ExportAuditEvents`' `200` is `{ "text/csv": string }`
 * (TASK-0054), the contract's first non-JSON response body, so without this
 * fallback `SuccessBody` below would resolve `never` for it (see `JsonOf`)
 * and `apiGet` would mistype the CSV payload as unreachable rather than
 * `string`. Deliberately narrower than a general "any content-type" helper:
 * it only widens the *response* side (`RequestBodyOf` below still calls
 * `JsonOf` directly, unchanged, because every request body in this contract
 * remains JSON).
 */
type ResponseBodyOf<Content> = Content extends { 'application/json': infer J }
  ? J
  : Content extends Record<string, infer V>
    ? V
    : never;

/**
 * The first successful (2xx) response body an operation declares, or `void`
 * for a `204 No Content` — `DELETE /admins/{id}/sessions` is the first
 * operation in this contract with no 2xx body at all, so without this
 * branch `SuccessBody` would resolve `never` for it and no caller could ever
 * type the result of calling `apiDelete`.
 */
export type SuccessBody<Op> = Op extends { responses: infer R }
  ? R extends { 200: { content: infer C } }
    ? ResponseBodyOf<C>
    : R extends { 201: { content: infer C } }
      ? ResponseBodyOf<C>
      : R extends { 204: unknown }
        ? void
        : never
  : never;

/**
 * Normalises `never` to `undefined`. `openapi-typescript` emits a literal
 * `never` for a declared-empty query/body/path/header position — distinct
 * from the property being absent — so a plain conditional match on that
 * position infers `never` itself rather than "nothing to pass". Tuple-wrapped
 * so this checks for `never` exactly, without a union distributing across
 * the conditional.
 */
type NeverToUndefined<T> = [T] extends [never] ? undefined : T;

/**
 * An operation's query parameters, or `undefined` when it declares none.
 * Matched against an optional `query?:` position so both a required query
 * object (e.g. `ping`) and an optional one (e.g. `records`) resolve to the
 * object's own shape rather than falling through to `undefined`.
 */
export type QueryOf<Op> = Op extends { parameters: { query?: infer Q } } ? NeverToUndefined<Q> : undefined;

/** An operation's JSON request body, or `undefined` when it declares none. */
export type RequestBodyOf<Op> = Op extends { requestBody?: { content: infer C } }
  ? NeverToUndefined<JsonOf<C>>
  : undefined;

/**
 * An operation's PATH parameters (e.g. `{ id: string }` for
 * `/admins/{id}`), or `undefined` when it declares none. Type-driven from
 * `operations[...]["parameters"]["path"]` — never a string the caller
 * formats by hand.
 */
type PathParamsOf<Op> = Op extends { parameters: { path?: infer PP } } ? NeverToUndefined<PP> : undefined;

/**
 * `Idempotency-Key`'s declared requiredness for one operation, read directly
 * off the header parameter shape. `X-CSRF-Token` deliberately never appears
 * here or anywhere below: it is transport-owned — `http-client.ts`'s request
 * interceptor injects it on every mutating request (see that file, the
 * `X-CSRF-Token` line in `attachAuthInterceptors`) — so the typed wrapper
 * must not offer a caller any way to set it, even though the schema declares
 * it a required header on every one of these operations. `Idempotency-Key`
 * is the opposite: caller-owned data, so its required/optional-ness must
 * come through exactly as the contract declares it, not widened to a
 * generic `Record<string, string>` headers bag that would blur the two.
 */
type IdempotencyKeyOf<Op> = Op extends { parameters: { header?: infer H } }
  ? H extends { 'Idempotency-Key': string }
    ? string
    : H extends { 'Idempotency-Key'?: string }
      ? string | undefined
      : undefined
  : undefined;

/**
 * The operation-specific fields folded into a verb helper's trailing options
 * bag, on top of the plain transport options (`signal`/`timeout`/`params`).
 * Each fragment resolves to `unknown` (a no-op in an intersection) when the
 * operation doesn't need it, so `WithExtras`/`OptionsArgs` below only ever
 * add a required field when the contract actually demands one.
 */
type RequestExtras<Op> = (PathParamsOf<Op> extends undefined
  ? unknown
  : { pathParams: PathParamsOf<Op> }) &
  (IdempotencyKeyOf<Op> extends undefined
    ? unknown
    : undefined extends IdempotencyKeyOf<Op>
      ? { idempotencyKey?: string }
      : { idempotencyKey: string });

type WithExtras<Op, Base> = Base & RequestExtras<Op>;

/** Does this operation demand an actual path-parameter value? */
type RequiresPath<Op> = PathParamsOf<Op> extends undefined ? false : true;

/** Does this operation demand an actual `Idempotency-Key` value? */
type RequiresIdempotencyKey<Op> = IdempotencyKeyOf<Op> extends undefined
  ? false
  : undefined extends IdempotencyKeyOf<Op>
    ? false
    : true;

/**
 * A verb helper's trailing options argument. `Base` (plain transport options)
 * never has a required field of its own, so the whole options bag becomes
 * REQUIRED exactly when the operation demands a path parameter or a required
 * `Idempotency-Key`, and stays optional otherwise. This is what makes a
 * caller omitting a required path param or a required `Idempotency-Key`
 * fail typecheck rather than silently compiling.
 */
export type OptionsArgs<Op, Base> = RequiresPath<Op> extends true
  ? [options: WithExtras<Op, Base>]
  : RequiresIdempotencyKey<Op> extends true
    ? [options: WithExtras<Op, Base>]
    : [options?: WithExtras<Op, Base>];

/**
 * The slice of `RequestOptions` a caller may set directly, minus `headers` —
 * the only header this layer lets a caller influence is `Idempotency-Key`,
 * threaded through the dedicated `idempotencyKey` field above instead, so
 * there is no back door to hand-set `X-CSRF-Token` or anything else.
 */
export type CallerOptions = Omit<RequestOptions, 'headers'>;
