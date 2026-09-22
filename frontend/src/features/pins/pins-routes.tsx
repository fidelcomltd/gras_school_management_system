import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { PinBatchScreen } from './pin-batch-screen';
import { PinsScreen } from './pins-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const pinsRoutes: RouteObject[] = [
  {
    path: 'pins',
    element: (
      <RequirePrivilege privilege="pin.view">
        <PinsScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'pins/:id',
    element: (
      <RequirePrivilege privilege="pin.view">
        <PinBatchScreen />
      </RequirePrivilege>
    ),
  },
];
