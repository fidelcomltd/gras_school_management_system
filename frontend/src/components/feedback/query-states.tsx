import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';

/**
 * The loading and error halves of CONVENTIONS.md §11's four required states, shared by every
 * data screen from the pupils feature on. (The older screens inline the same markup; they move
 * here when next touched rather than in a sweep.) Empty is screen-specific copy, so it stays in
 * the screen. Unauthorized renders nothing: `ProtectedLayout` is already navigating to sign-in.
 */
export function LoadingState({ label }: { label: string }) {
  return <output className="text-sm text-muted-foreground">{label}</output>;
}

export function QueryErrorState({ error, onRetry }: { error: Error; onRetry: () => void }) {
  if (error instanceof ApiError && error.kind === 'unauthorized') {
    return null;
  }

  return (
    <div role="alert" className="flex flex-col items-start gap-3">
      <p className="text-sm text-destructive">{error.message}</p>
      <Button variant="outline" size="sm" onClick={onRetry}>
        Try again
      </Button>
    </div>
  );
}

/** A server rejection shown at the top of a form, verbatim. */
export function FormError({ message }: { message: string | null | undefined }) {
  return message ? (
    <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
      {message}
    </p>
  ) : null;
}
