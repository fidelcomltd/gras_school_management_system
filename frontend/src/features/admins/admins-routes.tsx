import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { AdminDetailScreen } from './admin-detail-screen';
import { AdminsListScreen } from './admins-list-screen';

/**
 * This feature's slice of the route table (TASK-0043), mirroring
 * `features/sessions/sessions-routes.tsx` — mounted inside `ProtectedLayout`'s
 * `<Outlet/>`, so `path` is relative to that (pathless) layout route.
 */
export const adminsRoutes: RouteObject[] = [
  {
    path: 'admins',
    element: (
      <RequirePrivilege privilege="admin.view">
        <AdminsListScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'admins/:id',
    element: (
      <RequirePrivilege privilege="admin.view">
        <AdminDetailScreen />
      </RequirePrivilege>
    ),
  },
];
