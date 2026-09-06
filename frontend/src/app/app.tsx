import { useEffect } from 'react';
import { ErrorBoundary } from '@/components/feedback/error-boundary';
import { ensureCsrfToken } from '@/lib/http';
import { initTheme } from '@/stores/theme-store';
import { QueryProvider } from './providers/query-provider';
import { AppRouter } from './router/app-router';

/**
 * Composition root. Providers wrap the router; startup side effects are
 * registered here so nothing runs at module import time.
 */
export function App() {
  useEffect(() => {
    const disposers = [initTheme()];
    // Bootstraps the CSRF pair (sign-in is itself CSRF-protected) and, on a hard
    // reload while already signed in, rotates it to the live session's binding.
    // A failure here is not fatal — the http layer's single CSRF retry recovers
    // on the first mutating request either way.
    void ensureCsrfToken();
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
