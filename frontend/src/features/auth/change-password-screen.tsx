import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useSignOut } from './api';
import { ChangePasswordForm } from './components/change-password-form';

/** `/account/password`: a signed-in staff member changes their own password. */
export function ChangePasswordScreen() {
  return (
    <div className="flex w-full max-w-md flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Change password</h1>
        <p className="text-sm text-muted-foreground">Other devices signed in to your account will be signed out.</p>
      </header>
      <ChangePasswordForm />
    </div>
  );
}

/**
 * Shown on every protected route while the account must change its password (spec 6.1.11): the server refuses every
 * other request until it does, so there is nothing else to navigate to. Success replaces `me`, and the full app
 * renders at the address the user asked for.
 */
export function ForcedPasswordChange({ staffName }: { staffName: string }) {
  const signOut = useSignOut();
  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Set a new password</h1>
        <p className="text-sm text-muted-foreground">
          Welcome, {staffName}. Your account needs a new password before you can continue.
        </p>
      </header>
      <ChangePasswordForm showSuccess={false} />
      <Button variant="ghost" size="sm" className="self-start" onClick={() => signOut.mutate()} disabled={signOut.isPending}>
        Sign out instead
      </Button>
      {signOut.error instanceof ApiError ? (
        <p role="alert" className="text-xs text-destructive">
          {signOut.error.message}
        </p>
      ) : null}
    </div>
  );
}
