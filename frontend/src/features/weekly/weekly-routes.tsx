import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { WeeklyCompletionScreen } from './weekly-completion-screen';
import { WeeklyScreen } from './weekly-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const weeklyRoutes: RouteObject[] = [
  {
    path: 'weekly',
    element: (
      <RequirePrivilege privilege="weekly.view">
        <WeeklyScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'weekly/completion',
    element: (
      <RequirePrivilege privilege="report.view">
        <WeeklyCompletionScreen />
      </RequirePrivilege>
    ),
  },
];
