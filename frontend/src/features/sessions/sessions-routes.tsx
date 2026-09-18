import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { SessionDetailScreen } from './session-detail-screen';
import { SessionsListScreen } from './sessions-list-screen';

/**
 * This feature's slice of the route table (TASK-0042), mirroring
 * `features/settings/settings-routes.tsx` — mounted inside `ProtectedLayout`'s
 * `<Outlet/>`, so `path` is relative to that (pathless) layout route.
 */
export const sessionsRoutes: RouteObject[] = [
  {
    path: 'sessions',
    element: (
      <RequirePrivilege privilege="session.view">
        <SessionsListScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'sessions/:id',
    element: (
      <RequirePrivilege privilege="session.view">
        <SessionDetailScreen />
      </RequirePrivilege>
    ),
  },
];
