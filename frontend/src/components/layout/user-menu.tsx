import { Menu } from '@base-ui/react/menu';
import { ChevronDown, Lock, LogOut } from 'lucide-react';
import { useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { useSignOut } from '@/features/auth/api';
import type { AuthSession } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { roleLabel, roleNamesOf } from './user-role';

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return ((parts[0]?.[0] ?? '') + (parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : '')).toUpperCase() || '?';
}

const item =
  'flex w-full cursor-default items-center gap-2 rounded-md px-2 py-1.5 text-sm text-foreground outline-none data-[highlighted]:bg-muted';

/** The header's account section: who is signed in and as what, with the account actions behind it. */
export function UserMenu({ session }: { session: AuthSession }) {
  const signOut = useSignOut();
  const navigate = useNavigate();

  return (
    <Menu.Root>
      <Menu.Trigger
        aria-label={`Account: ${session.staffName}, ${roleLabel(session)}`}
        className="flex items-center gap-2 rounded-md px-1.5 py-1 text-left transition-colors hover:bg-muted"
      >
        <span aria-hidden="true" className="grid size-8 shrink-0 place-items-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
          {initials(session.staffName)}
        </span>
        <span className="hidden min-w-0 flex-col sm:flex">
          <span className="max-w-40 truncate text-sm leading-tight font-medium text-foreground">{session.staffName}</span>
          <span className="max-w-40 truncate text-xs leading-tight text-muted-foreground" title={roleNamesOf(session).join(', ')}>
            {roleLabel(session)}
          </span>
        </span>
        <ChevronDown className="hidden size-4 text-muted-foreground sm:block" aria-hidden="true" />
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Positioner sideOffset={6} align="end" className="z-50">
          <Menu.Popup className="w-60 rounded-lg border border-border bg-surface p-1 shadow-lg outline-none">
            <div className="px-2 py-1.5">
              <p className="truncate text-sm font-medium text-foreground">{session.staffName}</p>
              <p className="truncate text-xs text-muted-foreground">{session.email}</p>
              {roleNamesOf(session).length > 1 ? <p className="mt-1 text-xs text-muted-foreground">{roleNamesOf(session).join(', ')}</p> : null}
            </div>
            <Menu.Separator className="my-1 border-t border-border" />
            <Menu.Item className={item} onClick={() => void navigate(paths.changePassword)}>
              <Lock className="size-4 text-muted-foreground" aria-hidden="true" />
              Change password
            </Menu.Item>
            <Menu.Item className={item} closeOnClick={false} disabled={signOut.isPending} onClick={() => signOut.mutate()}>
              <LogOut className="size-4 text-muted-foreground" aria-hidden="true" />
              {signOut.isPending ? 'Signing out…' : 'Sign out'}
            </Menu.Item>
            {signOut.error instanceof ApiError ? (
              <p role="alert" className="px-2 pt-1 text-xs text-destructive">
                {signOut.error.message}
              </p>
            ) : null}
          </Menu.Popup>
        </Menu.Positioner>
      </Menu.Portal>
    </Menu.Root>
  );
}
