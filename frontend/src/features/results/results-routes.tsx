import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { ClassRecordsScreen } from './class-records-screen';
import { MarksScreen } from './marks-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const resultsRoutes: RouteObject[] = [
  {
    path: 'results/marks',
    element: (
      <RequirePrivilege privilege="result.view">
        <MarksScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'results/class',
    element: (
      <RequirePrivilege privilege="result.view">
        <ClassRecordsScreen />
      </RequirePrivilege>
    ),
  },
];
