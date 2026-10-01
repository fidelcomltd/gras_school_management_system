import type { ReactNode } from 'react';
import { ArrowLeft, ChevronRight, House } from 'lucide-react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { ApiError } from '@/lib/http';
import { canOpen, navItemFor } from './nav-config';

export interface TrailItem {
  /** Optional for a menu page: its label comes from the sidebar, so a rename reaches the trail too. */
  label?: string;
  /** Omit for the current page, which is always the last item. */
  to?: string;
}

/**
 * The top of a nested page: a back button to its parent page and the breadcrumb trail from Home. The back button follows
 * the trail, not the browser history, so it always goes to the same place, even when the page was opened from a link. A
 * page the caller cannot open (its menu privilege is missing) shows as plain text and is never the back target.
 */
export function PageTrail({ trail }: { trail: TrailItem[] }) {
  const me = useMe();
  const openable = (to: string | undefined): to is string => !!to && (!me.data || canOpen(me.data, to));
  const labelOf = (item: TrailItem) => item.label ?? (item.to ? navItemFor(item.to)?.label : undefined) ?? '';
  // The current page (the last item) is never its own way back.
  const parent = trail.slice(0, -1).findLast((item) => openable(item.to));

  return (
    <div className="flex items-center gap-3">
      {parent?.to ? (
        <Button
          variant="outline"
          size="icon"
          className="size-8 rounded-full"
          aria-label={`Back to ${labelOf(parent)}`}
          title={`Back to ${labelOf(parent)}`}
          render={<Link to={parent.to} />}
        >
          <ArrowLeft aria-hidden="true" />
        </Button>
      ) : null}
      <nav aria-label="Breadcrumb" className="min-w-0">
        <ol className="flex flex-wrap items-center gap-1 text-sm text-muted-foreground">
          <li className="flex items-center">
            <Link to={paths.root} className="inline-flex items-center rounded-sm p-0.5 hover:text-foreground">
              <House className="size-4" aria-hidden="true" />
              <span className="sr-only">Home</span>
            </Link>
          </li>
          {trail.map((item, index) => {
            const last = index === trail.length - 1;
            const label = labelOf(item);
            return (
              <li key={`${item.to ?? ''}|${label}`} className="flex min-w-0 items-center gap-1">
                <ChevronRight className="size-3.5 shrink-0" aria-hidden="true" />
                {!last && openable(item.to) ? (
                  <Link to={item.to} className="truncate rounded-sm hover:text-foreground hover:underline">
                    {label}
                  </Link>
                ) : last ? (
                  <span aria-current="page" className="max-w-64 truncate font-medium text-foreground">
                    {label}
                  </span>
                ) : (
                  <span className="truncate">{label}</span>
                )}
              </li>
            );
          })}
        </ol>
      </nav>
    </div>
  );
}

/** A nested page's loading or error state, still under its trail, so a stale link never strands the reader. */
export function WithTrail({ trail, children }: { trail: TrailItem[]; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-6">
      <PageTrail trail={trail} />
      {children}
    </div>
  );
}

/**
 * A nested page's load failure under its trail. Renders nothing for a lapsed session, like QueryErrorState: the
 * protected layout is already on its way to sign-in.
 */
export function TrailedError({ trail, error, onRetry }: { trail: TrailItem[]; error: Error; onRetry: () => void }) {
  if (error instanceof ApiError && error.kind === 'unauthorized') return null;
  return (
    <WithTrail trail={trail}>
      <QueryErrorState error={error} onRetry={onRetry} />
    </WithTrail>
  );
}
