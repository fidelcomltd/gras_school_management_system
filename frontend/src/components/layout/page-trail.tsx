import { ArrowLeft, ChevronRight, House } from 'lucide-react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';

export interface TrailItem {
  label: string;
  /** Omit for the current page, which is always the last item. */
  to?: string;
}

/**
 * The top of a nested page: a back button to its parent page and the breadcrumb trail from Home. The back button follows
 * the trail, not the browser history, so it always goes to the same place, even when the page was opened from a link.
 */
export function PageTrail({ trail }: { trail: TrailItem[] }) {
  const parent = trail.findLast((item) => item.to !== undefined);

  return (
    <div className="flex items-center gap-3">
      {parent?.to ? (
        <Link
          to={parent.to}
          aria-label={`Back to ${parent.label}`}
          title={`Back to ${parent.label}`}
          className="inline-flex size-8 shrink-0 items-center justify-center rounded-full border border-border bg-surface text-muted-foreground shadow-xs transition-colors hover:bg-muted hover:text-foreground"
        >
          <ArrowLeft className="size-4" aria-hidden="true" />
        </Link>
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
            const key = item.to ?? 'current';
            return (
              <li key={key} className="flex min-w-0 items-center gap-1">
                <ChevronRight className="size-3.5 shrink-0" aria-hidden="true" />
                {item.to && !last ? (
                  <Link to={item.to} className="truncate rounded-sm hover:text-foreground hover:underline">
                    {item.label}
                  </Link>
                ) : (
                  <span aria-current={last ? 'page' : undefined} className="max-w-64 truncate font-medium text-foreground">
                    {item.label}
                  </span>
                )}
              </li>
            );
          })}
        </ol>
      </nav>
    </div>
  );
}
