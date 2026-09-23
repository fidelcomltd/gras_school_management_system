import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { AdmissionsQueueScreen } from './admissions-queue-screen';
import { AdmissionFlowScreen } from './flow/admission-flow-screen';

/**
 * This feature's slice of the route table (TASK-0064), mirroring
 * `features/arms/arms-routes.tsx` — mounted inside `ProtectedLayout`'s
 * `<Outlet/>`, so `path` is relative to that (pathless) layout route.
 */
export const admissionsRoutes: RouteObject[] = [
  {
    path: 'admissions',
    element: (
      <RequirePrivilege privilege="pupil.view">
        <AdmissionsQueueScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'admissions/:id',
    element: (
      <RequirePrivilege privilege="pupil.view">
        <AdmissionFlowScreen />
      </RequirePrivilege>
    ),
  },
];
