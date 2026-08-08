import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';

/**
 * Catches render-time errors so one broken subtree cannot blank the app.
 *
 * Mount one per route. Error boundaries still have to be class components —
 * there is no hook equivalent of `componentDidCatch`.
 *
 * This does NOT catch errors in event handlers or async code. TanStack Query
 * failures surface through query state, not here.
 */

interface ErrorBoundaryProps {
  children: ReactNode;
  /** Rendered instead of the default panel. Receives the error and a reset callback. */
  fallback?: (error: Error, reset: () => void) => ReactNode;
  onError?: (error: Error, info: ErrorInfo) => void;
}

interface ErrorBoundaryState {
  error: Error | null;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  override state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  override componentDidCatch(error: Error, info: ErrorInfo): void {
    this.props.onError?.(error, info);
  }

  private readonly reset = (): void => {
    this.setState({ error: null });
  };

  override render(): ReactNode {
    const { error } = this.state;
    if (!error) return this.props.children;

    if (this.props.fallback) return this.props.fallback(error, this.reset);

    return (
      <div
        role="alert"
        className="mx-auto flex max-w-md flex-col items-start gap-3 rounded-xl border border-border bg-surface p-6"
      >
        <h2 className="font-display text-lg font-semibold text-foreground">
          Something went wrong
        </h2>
        <p className="text-sm text-muted-foreground">
          This part of the page could not be displayed. The rest of the portal is unaffected.
        </p>
        <Button variant="outline" size="sm" onClick={this.reset}>
          Try again
        </Button>
      </div>
    );
  }
}
