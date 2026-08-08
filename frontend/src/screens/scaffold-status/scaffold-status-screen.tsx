import { APP_ENV, API_BASE_URL } from '@/config/env-values';
import { PrimitiveGallery } from './components/primitive-gallery';
import { TokenSwatches } from './components/token-swatches';

/**
 * The only screen in the scaffold. It exists to prove the foundation renders
 * and to give the next session a working reference for screen composition.
 *
 * Delete it as soon as a real route exists.
 */
export function ScaffoldStatusScreen() {
  return (
    <div className="flex flex-col gap-12">
      <header className="flex flex-col gap-2">
        <p className="text-xs font-semibold tracking-wide text-accent-foreground uppercase">
          Scaffold
        </p>
        <h1 className="font-display text-3xl font-semibold text-foreground">
          Foundation is in place
        </h1>
        <p className="max-w-2xl text-sm text-muted-foreground">
          No feature code has been written. The design tokens, HTTP layer, primitives, and test
          suite below are the platform the next session builds on. Read{' '}
          <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">CONVENTIONS.md</code>{' '}
          first.
        </p>
        <dl className="mt-2 flex flex-wrap gap-x-8 gap-y-2 text-xs">
          <div>
            <dt className="inline text-muted-foreground">Environment: </dt>
            <dd className="inline font-mono text-foreground">{APP_ENV}</dd>
          </div>
          <div>
            <dt className="inline text-muted-foreground">API base: </dt>
            <dd className="inline font-mono text-foreground">{API_BASE_URL}</dd>
          </div>
        </dl>
      </header>

      <TokenSwatches />
      <PrimitiveGallery />
    </div>
  );
}
