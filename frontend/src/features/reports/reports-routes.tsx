import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { ReportScreen } from './report-screen';
import { ReportsScreen } from './reports-screen';

/** This feature's slice of the route table. The static report routes other features own rank above `reports/:key`. */
export const reportsRoutes: RouteObject[] = [
  {
    path: 'reports',
    element: (
      <RequirePrivilege privilege="report.view">
        <ReportsScreen />
      </RequirePrivilege>
    ),
  },
  {
    path: 'reports/:key',
    element: (
      <RequirePrivilege privilege="report.view">
        <ReportScreen />
      </RequirePrivilege>
    ),
  },
];
