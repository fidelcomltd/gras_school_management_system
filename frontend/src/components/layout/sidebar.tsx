import { Lock, LogOut } from 'lucide-react';
import { Link, NavLink } from 'react-router';
import { paths } from '@/app/router/paths';
import { useSignOut } from '@/features/auth/api';
import type { AuthSession } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { cn } from '@/lib/utils/cn';
import { Brand } from './app-shell';
import { visibleNavGroups } from './nav-config';

const footerAction =
  'flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:opacity-50';

/** The sidebar body, shared by the fixed desktop column and the mobile drawer. */
export function Sidebar({ session, onNavigate }: { session: AuthSession; onNavigate?: () => void }) {
  const signOut = useSignOut();

  return (
    <div className="flex h-full flex-col">
      <div className="flex h-16 shrink-0 items-center border-b border-border pr-12 pl-4 lg:pr-4">
        <Brand />
      </div>

      <nav aria-label="Main" className="flex-1 overflow-y-auto px-3 py-4">
        {visibleNavGroups(session).map((group) => (
          <div key={group.label ?? 'top'} className="mb-5 last:mb-0">
            {group.label ? (
              <p className="mb-1 px-3 text-[11px] font-semibold tracking-wider text-muted-foreground uppercase">
                {group.label}
              </p>
            ) : null}
            <ul className="flex flex-col gap-0.5">
              {group.items.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.end ?? false}
                    onClick={onNavigate}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                        isActive ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                      )
                    }
                  >
                    <item.icon className="size-4 shrink-0" aria-hidden="true" />
                    {item.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div className="shrink-0 border-t border-border p-3">
        <p className="truncate px-3 text-sm font-medium text-foreground">{session.staffName}</p>
        <p className="mb-2 truncate px-3 text-xs text-muted-foreground">{session.email}</p>
        <Link to={paths.changePassword} onClick={onNavigate} className={footerAction}>
          <Lock className="size-4" aria-hidden="true" />
          Change password
        </Link>
        <button type="button" onClick={() => signOut.mutate()} disabled={signOut.isPending} className={footerAction}>
          <LogOut className="size-4" aria-hidden="true" />
          {signOut.isPending ? 'Signing out…' : 'Sign out'}
        </button>
        {signOut.error instanceof ApiError ? (
          <p role="alert" className="px-3 pt-1 text-xs text-destructive">
            {signOut.error.message}
          </p>
        ) : null}
      </div>
    </div>
  );
}
