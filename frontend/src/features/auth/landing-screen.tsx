import { Navigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useMe } from './api';

/**
 * `/` — the minimum protected landing (TASK-0021 ruling 5), now mounted
 * inside `ProtectedLayout`'s `<Outlet/>` (TASK-0041): the persistent nav and
 * its one Sign-out control live in `AuthenticatedShell`, not here, so there
 * is exactly one Sign-out affordance on screen rather than a second,
 * redundant one. `ProtectedLayout` also owns the "session ended → sign-in"
 * subscription for every protected route now, not just this one.
 *
 * Four required states (CONVENTIONS.md §11) for the `GET /auth/me` fetch this
 * screen makes: loading, error, unauthorized, and success below. There is no
 * distinct "empty" state — `me` is a single-object profile fetch that is either
 * present on success or absent as one of the other three; a list-shaped empty
 * state does not apply here.
 */
export function LandingScreen() {
  const me = useMe();

  if (me.isPending) {
    return (
      <output className="text-sm text-muted-foreground">Loading your account…</output>
    );
  }

  if (me.isError) {
    if (me.error instanceof ApiError && me.error.kind === 'unauthorized') {
      return <Navigate to={paths.signIn} replace />;
    }
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{me.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void me.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const session = me.data;

  return (
    <div className="flex flex-col items-start gap-4">
      <h1 className="font-display text-2xl font-semibold text-foreground">
        Welcome, {session.staffName}
      </h1>
    </div>
  );
}
