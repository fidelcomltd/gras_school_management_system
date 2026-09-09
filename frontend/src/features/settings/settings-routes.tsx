import type { RouteObject } from 'react-router';
import { RequirePrivilege } from '@/app/router/require-privilege';
import { SettingsScreen } from './settings-screen';

/**
 * This feature's slice of the route table (TASK-0041) — mounted inside
 * `ProtectedLayout`'s `<Outlet/>`, so its `path` is relative to that
 * (pathless) layout route, not an absolute string: React Router resolves a
 * nested route's `path` against its parent's own matched segment, and the
 * parent here contributes none. Kept in sync with `paths.settings` ('/settings')
 * by construction — `AuthenticatedShell`'s nav links use `paths.settings`
 * itself, this only needs the same segment without the leading slash.
 */
export const settingsRoutes: RouteObject[] = [
  {
    path: 'settings',
    element: (
      <RequirePrivilege privilege="settings.view">
        <SettingsScreen />
      </RequirePrivilege>
    ),
  },
];
