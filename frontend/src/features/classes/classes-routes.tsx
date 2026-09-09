import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { ClassesScreen } from './classes-screen';

/**
 * This feature's slice of the route table (TASK-0042), mirroring
 * `features/settings/settings-routes.tsx` — mounted inside `ProtectedLayout`'s
 * `<Outlet/>`, so `path` is relative to that (pathless) layout route.
 */
export const classesRoutes: RouteObject[] = [
  {
    path: 'classes',
    element: (
      <RequirePrivilege privilege="level.view">
        <ClassesScreen />
      </RequirePrivilege>
    ),
  },
];
