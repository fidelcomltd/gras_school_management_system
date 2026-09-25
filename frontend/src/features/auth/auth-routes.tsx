import type { RouteObject } from 'react-router';
import { paths } from '@/app/router/paths';
import { ErrorBoundary } from '@/components/feedback/error-boundary';
import { AppShell } from '@/components/layout/app-shell';
import { ChangePasswordScreen } from './change-password-screen';
import { LandingScreen } from './landing-screen';
import { SignInScreen } from './sign-in-screen';

/**
 * This feature's slice of the route table (TASK-0041) — `app-router.tsx`
 * spreads these in rather than inlining them, per its own comment that said
 * to split per-feature once there was more than a handful of routes.
 */

/** Public — no session required. Top-level, outside `ProtectedLayout`. */
export const publicAuthRoutes: RouteObject[] = [
  {
    path: paths.signIn,
    element: (
      <AppShell>
        <ErrorBoundary>
          <SignInScreen />
        </ErrorBoundary>
      </AppShell>
    ),
  },
];

/** Protected — mounted inside `ProtectedLayout`'s `<Outlet/>`. */
export const authRoutes: RouteObject[] = [
  { index: true, element: <LandingScreen /> },
  { path: paths.changePassword, element: <ChangePasswordScreen /> },
];
