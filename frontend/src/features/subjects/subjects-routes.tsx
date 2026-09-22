import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { MappingScreen } from './mapping-screen';
import { SubjectsScreen } from './subjects-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const subjectsRoutes: RouteObject[] = [
  {
    path: 'subjects',
    element: (
      <RequirePrivilege privilege="subject.view">
        <SubjectsScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'subjects/mapping',
    element: (
      <RequirePrivilege privilege="subject.view">
        <MappingScreen />
      </RequirePrivilege>
    ),
  },
];
