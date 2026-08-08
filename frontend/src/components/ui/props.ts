/**
 * Swaps a Base UI component's `className` (which also accepts a state
 * callback) for a plain optional string, so `cn()` can merge it.
 *
 * The explicit `| undefined` is required by `exactOptionalPropertyTypes`:
 * without it, spreading a prop that happens to be undefined is a type error at
 * every call site.
 */
export type WithClassName<T> = Omit<T, 'className'> & {
  className?: string | undefined;
};
