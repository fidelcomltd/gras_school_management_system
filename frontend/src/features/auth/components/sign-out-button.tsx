import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useSignOut } from '../api';

/**
 * Server-authoritative sign-out (root CLAUDE.md §5): the mutation revokes the
 * session server-side first; only its `onSuccess` (in `../api.ts`) ends the
 * client-side session. Clearing client state without the server call is a
 * blocker and is not something this component can do even by accident — there
 * is no local "just clear it" path here.
 */
export function SignOutButton() {
  const signOut = useSignOut();

  return (
    <div className="flex flex-col items-start gap-2">
      <Button variant="outline" onClick={() => signOut.mutate()} disabled={signOut.isPending}>
        {signOut.isPending ? 'Signing out…' : 'Sign out'}
      </Button>
      {signOut.error instanceof ApiError ? (
        <p role="alert" className="text-xs text-destructive">
          {signOut.error.message}
        </p>
      ) : null}
    </div>
  );
}
