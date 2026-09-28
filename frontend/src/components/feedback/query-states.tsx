import { useEffect, useRef } from 'react';
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
  // A server's answer must be seen wherever the user is (lead, 2026-09-28): a long form's submit button sits far below
  // this banner, so it scrolls into view when a message arrives and then stays pinned under the page header (at the
  // top inside a dialog) while the user scrolls back to fix the field.
  const ref = useRef<HTMLParagraphElement>(null);
  useEffect(() => {
    if (message) ref.current?.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' });
  }, [message]);
  return message ? (
    <p
      ref={ref}
      role="alert"
      className="sticky top-20 z-10 scroll-mt-20 rounded-md border border-destructive/30 bg-destructive-subtle px-3 py-2 text-sm text-destructive shadow-sm in-[[role=dialog]]:top-0 in-[[role=dialog]]:scroll-mt-0"
    >
      {message}
    </p>
  ) : null;
}
