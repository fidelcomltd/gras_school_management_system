import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { FeeNoticesScreen } from './fee-notices-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const feesRoutes: RouteObject[] = [
  {
    path: 'fees',
    element: (
      <RequirePrivilege privilege="fee.manage">
        <FeeNoticesScreen />
      </RequirePrivilege>
    ),
  },
];
