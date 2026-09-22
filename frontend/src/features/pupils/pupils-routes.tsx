import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { PupilDetailScreen } from './pupil-detail-screen';
import { PupilsListScreen } from './pupils-list-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const pupilsRoutes: RouteObject[] = [
  {
    path: 'pupils',
    element: (
      <RequirePrivilege privilege="pupil.view">
        <PupilsListScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'pupils/:id',
    element: (
      <RequirePrivilege privilege="pupil.view">
        <PupilDetailScreen />
      </RequirePrivilege>
    ),
  },
];
