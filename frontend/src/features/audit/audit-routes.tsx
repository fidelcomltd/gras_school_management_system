import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { AuditScreen } from './audit-screen';

/** This feature's slice of the route table, mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const auditRoutes: RouteObject[] = [
  {
    path: 'audit',
    element: (
      <RequirePrivilege privilege="audit.view">
        <AuditScreen />
      </RequirePrivilege>
    ),
  },
];
