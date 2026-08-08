import { useEffect } from 'react';
import { ErrorBoundary } from '@/components/feedback/error-boundary';
import { connectSessionExpiry } from '@/stores/session-store';
import { initTheme } from '@/stores/theme-store';
import { QueryProvider } from './providers/query-provider';
import { AppRouter } from './router/app-router';

/**
 * Composition root. Providers wrap the router; startup side effects are
 * registered here so nothing runs at module import time.
 */
export function App() {
  useEffect(() => {
    const disposers = [initTheme(), connectSessionExpiry()];
    return () => {
      for (const dispose of disposers) dispose();
    };
  }, []);

  return (
    <ErrorBoundary>
      <QueryProvider>
        <AppRouter />
      </QueryProvider>
    </ErrorBoundary>
  );
}
