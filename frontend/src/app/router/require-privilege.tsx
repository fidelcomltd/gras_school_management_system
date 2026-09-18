import type { ReactNode } from 'react';
import { ForbiddenScreen } from '@/components/feedback/forbidden-screen';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';

/**
 * Per-route privilege gate (TASK-0041 AC-3): renders `children` only if the
 * signed-in caller holds `privilege`, otherwise `ForbiddenScreen` — a 403,
 * never the 404 fallback, since the route genuinely exists.
 *
 * Only ever mounted inside `ProtectedLayout`'s `<Outlet/>`, where `GET
 * /auth/me` has already resolved successfully — `useMe()` here is a cache
 * read, not a new request. The `!me.data` branch is defensive only, for the
 * one render tick before that cache is populated.
 */
export function RequirePrivilege({ privilege, children }: { privilege: string; children: ReactNode }) {
  const me = useMe();

  if (!me.data) return null;

  return hasPrivilege(me.data, privilege) ? <>{children}</> : <ForbiddenScreen />;
}
