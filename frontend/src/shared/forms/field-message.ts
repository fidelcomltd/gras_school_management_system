/**
 * Tolerant lookup of a 422's server-supplied field errors onto a form field —
 * promoted here from two duplicated copies (`features/auth/components/sign-
 * in-form.tsx`, `features/settings/components/school-identity-form.tsx`) on
 * this feature's third use, per CONVENTIONS.md §4's "promote only in its own
 * commit, on the second/third genuine consumer" and TASK-0042's own
 * instruction to do so as a focused change.
 *
 * Case-insensitive because ASP.NET model-validation keys are PascalCase
 * (`Email`) while the rest of the wire is camelCase (`email`) — never assume
 * which casing a given 422 uses, always look it up.
 */
export function fieldMessage(
  fieldErrors: Record<string, string[]> | undefined,
  name: string,
): string | undefined {
  if (!fieldErrors) return undefined;
  const key = Object.keys(fieldErrors).find((candidate) => candidate.toLowerCase() === name.toLowerCase());
  return key ? fieldErrors[key]?.[0] : undefined;
}

/**
 * Whether any of `names` has a mapped message in `fieldErrors`. Used to
 * decide whether a general error banner is still needed alongside per-field
 * messages: a business-rule rejection that does not name one of the form's
 * own fields (for example one of the level progression chain rules, or a
 * term-transition precondition) must still reach the user verbatim rather
 * than being silently swallowed because it didn't match a known field.
 */
export function hasFieldError(fieldErrors: Record<string, string[]> | undefined, names: readonly string[]): boolean {
  return names.some((name) => fieldMessage(fieldErrors, name) !== undefined);
}
