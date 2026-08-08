import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement, ReactNode } from 'react';

/**
 * Renders a component inside the providers it needs in production.
 *
 * Use this instead of RTL's bare `render` for anything that touches server
 * state, so a missing provider surfaces here rather than in the browser.
 */

function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      // Retries turn a deliberate error-path test into a multi-second timeout.
      queries: { retry: false, gcTime: 0, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

export interface RenderWithProvidersResult extends RenderResult {
  queryClient: QueryClient;
  user: ReturnType<typeof userEvent.setup>;
}

export function renderWithProviders(
  ui: ReactElement,
  options: Omit<RenderOptions, 'wrapper'> & { queryClient?: QueryClient } = {},
): RenderWithProvidersResult {
  const { queryClient = createTestQueryClient(), ...renderOptions } = options;

  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }

  return {
    ...render(ui, { wrapper: Wrapper, ...renderOptions }),
    queryClient,
    user: userEvent.setup(),
  };
}

export * from '@testing-library/react';
export { userEvent };
