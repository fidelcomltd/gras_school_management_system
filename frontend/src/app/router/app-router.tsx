import { createBrowserRouter, RouterProvider } from 'react-router';
import { ErrorBoundary } from '@/components/feedback/error-boundary';
import { AppShell } from '@/components/layout/app-shell';
import { NotFoundScreen } from '@/components/feedback/not-found-screen';
import { admissionsRoutes } from '@/features/admissions/admissions-routes';
import { adminsRoutes } from '@/features/admins/admins-routes';
import { armsRoutes } from '@/features/arms/arms-routes';
import { authRoutes, publicAuthRoutes } from '@/features/auth/auth-routes';
import { classesRoutes } from '@/features/classes/classes-routes';
import { rolesRoutes } from '@/features/roles/roles-routes';
import { sessionsRoutes } from '@/features/sessions/sessions-routes';
import { settingsRoutes } from '@/features/settings/settings-routes';
import { ProtectedLayout } from './protected-layout';

/**
 * Route table, split per feature (TASK-0041) — this file's own prior comment
 * said to do that once there was more than a handful of routes, and this
 * card crossed that line. Every literal path string still lives in
 * `paths.ts`; each feature's own `*-routes.tsx` only refers to it.
 *
 * `ProtectedLayout` is the one route guard: unauthenticated → sign-in,
 * authenticated → `AuthenticatedShell` wrapping every protected route's
 * `<Outlet/>`, each already wrapped in its own `ErrorBoundary` there.
 */
const router = createBrowserRouter([
  ...publicAuthRoutes,
  {
    element: <ProtectedLayout />,
    children: [
      ...authRoutes,
      ...settingsRoutes,
      ...sessionsRoutes,
      ...classesRoutes,
      ...armsRoutes,
      ...admissionsRoutes,
      ...adminsRoutes,
      ...rolesRoutes,
    ],
  },
  {
    path: '*',
    element: (
      <AppShell>
        <ErrorBoundary>
          <NotFoundScreen />
        </ErrorBoundary>
      </AppShell>
    ),
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}
