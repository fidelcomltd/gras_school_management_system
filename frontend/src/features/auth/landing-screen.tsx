import { useEffect } from 'react';
import { Navigate, useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { onSessionEnded } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useMe } from './api';
import { SignOutButton } from './components/sign-out-button';

/**
 * `/` — the minimum protected landing (TASK-0021 ruling 5): no nav, no
 * dashboard, no widgets, just proof that the session and logout paths work.
 *
 * Four required states (CONVENTIONS.md §11) for the `GET /auth/me` fetch this
 * screen makes: loading, error, unauthorized, and success below. There is no
 * distinct "empty" state — `me` is a single-object profile fetch that is either
 * present on success or absent as one of the other three; a list-shaped empty
 * state does not apply here.
 */
export function LandingScreen() {
  const navigate = useNavigate();
  const me = useMe();

  // Covers a session that ends while this screen is already mounted and its
  // cached `me` data is still considered fresh (e.g. the proactive keepalive
  // hits the absolute cap, or a later request 401s) — `me`'s own query error
  // branch below only fires when `me` itself is (re)fetched.
  useEffect(() => onSessionEnded(() => void navigate(paths.signIn, { replace: true })), [navigate]);

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

  if (session.mustChangePassword) {
    return (
      <div className="flex flex-col items-start gap-4">
        <p className="text-sm text-foreground">
          You must change your password before continuing.
        </p>
        <SignOutButton />
      </div>
    );
  }

  return (
    <div className="flex flex-col items-start gap-4">
      <h1 className="font-display text-2xl font-semibold text-foreground">
        Welcome, {session.staffName}
      </h1>
      <SignOutButton />
    </div>
  );
}
