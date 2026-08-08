import { setupServer } from 'msw/node';
import { handlers } from './handlers';

/**
 * Shared MSW server. Lifecycle is managed in `src/test/setup.ts`, so tests only
 * ever call `server.use(...)` to add case-specific handlers.
 */
export const server = setupServer(...handlers);
