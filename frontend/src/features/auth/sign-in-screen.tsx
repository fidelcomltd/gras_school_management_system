import { APP_NAME } from '@/config/env-values';
import { SignInForm } from './components/sign-in-form';

/**
 * Public — the one route that does not require a session. `/` redirects here
 * whenever `GET /auth/me` reports the caller is unauthenticated.
 */
export function SignInScreen() {
  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-8">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Sign in</h1>
        <p className="text-sm text-muted-foreground">Sign in to {APP_NAME} with your staff account.</p>
      </header>
      <SignInForm />
    </div>
  );
}
