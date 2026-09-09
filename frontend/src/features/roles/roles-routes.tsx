import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { RolesListScreen } from './roles-list-screen';

/**
 * This feature's slice of the route table (TASK-0043), mirroring
 * `features/classes/classes-routes.tsx` — one screen, no separate detail
 * route (role editing happens in a dialog, like level editing does).
 */
export const rolesRoutes: RouteObject[] = [
  {
    path: 'roles',
    element: (
      <RequirePrivilege privilege="role.view">
        <RolesListScreen />
      </RequirePrivilege>
    ),
  },
];
