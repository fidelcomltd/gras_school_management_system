import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { ApiError } from '@/lib/http';
import { cn } from '@/lib/utils/cn';

/**
 * The loading and error halves of CONVENTIONS.md §11's four required states, shared by every data screen (the
 * 2026-09-26 UI pass moved the older screens' inline copies here). The empty half is `EmptyState`, with
 * screen-specific copy. Unauthorized renders nothing: `ProtectedLayout` is already navigating to sign-in.
 */
export function LoadingState({ label, className }: { label: string; className?: string }) {
  return (
    <output className={cn('flex items-center justify-center gap-3 py-10 text-sm text-muted-foreground', className)}>
      <Spinner />
      {label}
    </output>
  );
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
