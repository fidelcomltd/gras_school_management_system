import type { AuthSession } from '@/lib/auth/auth-session';

/**
 * The session's role names. Read defensively: an API older than this field (production until it is redeployed) omits
 * it, and a missing role label must never take the header down.
 */
export function roleNamesOf(session: AuthSession): string[] {
  const names: unknown = (session as Partial<AuthSession>).roleNames;
  return Array.isArray(names) ? names.filter((name): name is string => typeof name === 'string') : [];
}

/** "Class Teacher", "Class Teacher +1" for several roles, or a plain note when none is active. */
export function roleLabel(session: AuthSession): string {
  const [first, ...rest] = roleNamesOf(session);
  if (!first) return 'No role assigned';
  return rest.length > 0 ? `${first} +${rest.length}` : first;
}
