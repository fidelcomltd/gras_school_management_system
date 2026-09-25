import { useEffect } from 'react';
import { Navigate, Outlet, useNavigate } from 'react-router';
import { ErrorBoundary } from '@/components/feedback/error-boundary';
import { AppShell } from '@/components/layout/app-shell';
import { AuthenticatedShell } from '@/components/layout/authenticated-shell';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { ForcedPasswordChange } from '@/features/auth/change-password-screen';
import { onSessionEnded } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { paths } from './paths';

/**
 * The route guard TASK-0041 asks for: unauthenticated → sign-in, authenticated
 * → the back-office shell wrapping every protected route's `<Outlet/>`. The
 * single home for what happens while `GET /auth/me` is in flight, fails, or
 * reports no session, for every protected route at once — an individual
 * screen only ever mounts once this has already resolved to a session.
 *
 * Also the single subscriber (per route tree) to a session ending later —
 * the proactive keepalive lapsing, or a reactive 401 from any request —
 * so every protected screen gets the same "session ended → sign-in" behaviour
 * with no per-screen wiring of its own.
 */
export function ProtectedLayout() {
  const navigate = useNavigate();
  const me = useMe();

  useEffect(() => onSessionEnded(() => void navigate(paths.signIn, { replace: true })), [navigate]);

  if (me.isPending) {
    return (
      <AppShell>
        <output className="text-sm text-muted-foreground">Loading your account…</output>
      </AppShell>
    );
  }

  if (me.isError) {
    if (me.error instanceof ApiError && me.error.kind === 'unauthorized') {
      return <Navigate to={paths.signIn} replace />;
    }
    return (
      <AppShell>
        <div role="alert" className="flex flex-col items-start gap-3">
          <p className="text-sm text-destructive">{me.error.message}</p>
          <Button variant="outline" size="sm" onClick={() => void me.refetch()}>
            Try again
          </Button>
        </div>
      </AppShell>
    );
  }

  // The server refuses everything else until the password changes (403 auth.password_change_required), so no nav.
  if (me.data.mustChangePassword) {
    return (
      <AppShell>
        <ForcedPasswordChange staffName={me.data.staffName} />
      </AppShell>
    );
  }

  return (
    <AuthenticatedShell session={me.data}>
      <ErrorBoundary>
        <Outlet />
      </ErrorBoundary>
    </AuthenticatedShell>
  );
}
