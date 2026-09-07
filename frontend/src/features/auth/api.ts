import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import { terminateSession } from '@/lib/auth/auth-session';
import { AuthKeys, type SignInCommand } from './types';

/**
 * One hook per endpoint, per CONVENTIONS.md §4/`src/features/README.md`. None of
 * these call `setSession` themselves — `src/lib/http/http-client.ts`'s response
 * interceptor already arms the session/keepalive state from every
 * `AuthSessionResponse` (sign-in, me, refresh, password), so a feature hook stays
 * a plain typed query/mutation with no auth plumbing of its own.
 */

const ME_PATH = '/api/v1/auth/me';
const SIGN_IN_PATH = '/api/v1/auth/sign-in';
const SIGN_OUT_PATH = '/api/v1/auth/sign-out';

/** The caller's own account + session state. 401s when nobody is signed in. */
export function useMe() {
  return useQuery({
    queryKey: [AuthKeys.Me],
    queryFn: ({ signal }) => apiGet(ME_PATH, undefined, { signal }),
  });
}

export function useSignIn() {
  return useMutation({
    mutationKey: [AuthKeys.SignIn],
    mutationFn: (payload: SignInCommand) => apiPost(SIGN_IN_PATH, payload),
  });
}

/** Server-authoritative sign-out: revokes first, THEN the client state ends. */
export function useSignOut() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AuthKeys.SignOut],
    mutationFn: () => apiPost(SIGN_OUT_PATH, undefined),
    onSuccess: () => {
      terminateSession();
      queryClient.removeQueries({ queryKey: [AuthKeys.Me] });
    },
  });
}
