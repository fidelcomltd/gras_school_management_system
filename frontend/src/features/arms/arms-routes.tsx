import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { ArmDetailScreen } from './arm-detail-screen';
import { ArmsListScreen } from './arms-list-screen';

/**
 * This feature's slice of the route table (TASK-0045), mirroring
 * `features/sessions/sessions-routes.tsx` — mounted inside `ProtectedLayout`'s
 * `<Outlet/>`, so `path` is relative to that (pathless) layout route.
 */
export const armsRoutes: RouteObject[] = [
  {
    path: 'arms',
    element: (
      <RequirePrivilege privilege="arm.view">
        <ArmsListScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'arms/:id',
    element: (
      <RequirePrivilege privilege="arm.view">
        <ArmDetailScreen />
      </RequirePrivilege>
    ),
  },
];
