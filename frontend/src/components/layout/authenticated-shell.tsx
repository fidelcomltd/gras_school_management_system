import type { ReactNode } from 'react';
import { NavLink } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useSignOut } from '@/features/auth/api';
import { hasPrivilege, type AuthSession } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { cn } from '@/lib/utils/cn';
import { AppShell } from './app-shell';

/**
 * Wraps `AppShell` for every authenticated route (TASK-0041) — it does not
 * fork a second shell, it adds the persistent nav row `AppShell`'s own
 * comment says is deliberately absent until a feature needs one.
 *
 * Nav items are filtered by the caller's privileges from `GET /auth/me`: an
 * item the caller cannot use is ABSENT, never rendered disabled (AC-2). Add a
 * feature's nav entry here as its route lands.
 */
interface NavItem {
  label: string;
  to: string;
  /** Privilege code required to see this item. Omit for "any signed-in caller". */
  requires?: string;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Home', to: paths.root },
  { label: 'Sessions', to: paths.sessions, requires: 'session.view' },
  { label: 'Classes', to: paths.classes, requires: 'level.view' },
  { label: 'Arms', to: paths.arms, requires: 'arm.view' },
  { label: 'Pupils', to: paths.pupils, requires: 'pupil.view' },
  { label: 'Subjects', to: paths.subjects, requires: 'subject.view' },
  { label: 'Results', to: paths.results, requires: 'result.view' },
  { label: 'Marks', to: paths.marks, requires: 'result.view' },
  { label: 'Class records', to: paths.classRecords, requires: 'result.view' },
  { label: 'Admins', to: paths.admins, requires: 'admin.view' },
  { label: 'Roles', to: paths.roles, requires: 'role.view' },
  { label: 'Settings', to: paths.settings, requires: 'settings.view' },
];

export function AuthenticatedShell({
  session,
  children,
}: {
  session: AuthSession;
  children: ReactNode;
}) {
  const signOut = useSignOut();
  const items = NAV_ITEMS.filter((item) => !item.requires || hasPrivilege(session, item.requires));

  return (
    <AppShell>
      <div className="flex flex-col gap-6">
        <div className="flex flex-wrap items-center justify-between gap-4 border-b border-border pb-4">
          <nav aria-label="Main">
            <ul className="flex flex-wrap gap-4">
              {items.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.to === paths.root || item.to === paths.results}
                    className={({ isActive }) =>
                      cn(
                        'text-sm font-medium transition-colors',
                        isActive ? 'text-primary' : 'text-muted-foreground hover:text-foreground',
                      )
                    }
                  >
                    {item.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>

          <div className="flex items-center gap-3">
            <span className="text-sm text-foreground">{session.staffName}</span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => signOut.mutate()}
              disabled={signOut.isPending}
            >
              {signOut.isPending ? 'Signing out…' : 'Sign out'}
            </Button>
          </div>
        </div>

        {signOut.error instanceof ApiError ? (
          <p role="alert" className="text-xs text-destructive">
            {signOut.error.message}
          </p>
        ) : null}

        {children}
      </div>
    </AppShell>
  );
}
