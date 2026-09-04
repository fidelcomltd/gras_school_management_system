import { createBrowserRouter, RouterProvider } from 'react-router';
import { ErrorBoundary } from '@/components/feedback/error-boundary';
import { AppShell } from '@/components/layout/app-shell';
import { ScaffoldStatusScreen } from '@/screens/scaffold-status/scaffold-status-screen';
import { paths } from './paths';

/**
 * Route table. Each route wraps its element in an `ErrorBoundary` so a crash in
 * one screen never blanks the shell.
 *
 * Add feature routes here and their paths to `paths.ts`. Once there is more than
 * a handful, split into per-feature route arrays and spread them in.
 */
const router = createBrowserRouter([
  {
    path: paths.root,
    element: (
      <AppShell>
        <ErrorBoundary>
          <ScaffoldStatusScreen />
        </ErrorBoundary>
      </AppShell>
    ),
  },
  {
    path: '*',
    element: (
      <AppShell>
        <ErrorBoundary>
          <div className="flex flex-col items-start gap-2">
            <h1 className="font-display text-2xl font-semibold text-foreground">Page not found</h1>
            <p className="text-sm text-muted-foreground">
              The address you followed does not match any screen in the portal.
            </p>
          </div>
        </ErrorBoundary>
      </AppShell>
    ),
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}
