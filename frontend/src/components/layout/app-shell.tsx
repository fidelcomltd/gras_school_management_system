import type { ReactNode } from 'react';
import { ThemeToggle } from '@/components/theme/theme-toggle';
import { APP_NAME } from '@/config/env-values';

/**
 * Outer chrome for every authenticated screen: skip link, header, main region,
 * footer. Navigation is deliberately absent — there is nothing to navigate to
 * until features land, and inventing an information architecture now would
 * pre-empt a product decision.
 */
export function AppShell({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-background">
      <a
        href="#main"
        className="sr-only rounded-md bg-primary px-4 py-2 text-primary-foreground focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-50"
      >
        Skip to main content
      </a>

      <header className="sticky top-0 z-40 border-b border-border bg-surface/85 backdrop-blur">
        <div className="mx-auto flex h-16 w-full max-w-6xl items-center gap-3 px-4 sm:px-6">
          <Crest />
          <div className="min-w-0 flex-1">
            <p className="truncate font-display text-base font-semibold text-foreground">
              {APP_NAME}
            </p>
            <p className="truncate text-xs text-muted-foreground">Sailing into greatness</p>
          </div>
          <ThemeToggle />
        </div>
      </header>

      <main id="main" className="mx-auto w-full max-w-6xl flex-1 px-4 py-8 sm:px-6">
        {children}
      </main>

      <footer className="border-t border-border py-6">
        <p className="mx-auto max-w-6xl px-4 text-xs text-muted-foreground sm:px-6">
          {APP_NAME}
        </p>
      </footer>
    </div>
  );
}

/**
 * Placeholder mark. Swap for the real crest once the asset is committed to
 * `public/` — see CONVENTIONS.md §8.
 */
export function Crest() {
  return (
    <span
      aria-hidden="true"
      className="grid size-9 shrink-0 place-items-center rounded-md bg-primary font-display text-sm font-bold text-primary-foreground ring-2 ring-accent"
    >
      GRA
    </span>
  );
}
