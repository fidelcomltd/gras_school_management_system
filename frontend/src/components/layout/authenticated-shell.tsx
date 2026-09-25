import {
  BookOpen,
  CalendarCheck,
  CalendarRange,
  ChartColumn,
  FileWarning,
  House,
  KeyRound,
  Layers,
  LayoutGrid,
  Lock,
  LogOut,
  Menu,
  NotebookTabs,
  PenLine,
  ScrollText,
  Settings,
  ShieldCheck,
  ShieldUser,
  UserPlus,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link, NavLink } from 'react-router';
import { paths } from '@/app/router/paths';
import { ThemeToggle } from '@/components/theme/theme-toggle';
import { Button } from '@/components/ui/button';
import { APP_NAME } from '@/config/env-values';
import { useSignOut } from '@/features/auth/api';
import { hasPrivilege, type AuthSession } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { cn } from '@/lib/utils/cn';
import { Crest } from './app-shell';

/**
 * The back office's frame for every authenticated route (TASK-0041): a grouped sidebar on wide screens, a drawer on
 * narrow ones, and a top bar with the theme toggle.
 *
 * Nav items are filtered by the caller's privileges from `GET /auth/me`: an item the caller cannot use is ABSENT, never
 * rendered disabled (AC-2), and a group left empty is dropped. Add a feature's nav entry here as its route lands, gated
 * by the same privilege as its route.
 */
interface NavItem {
  label: string;
  to: string;
  icon: LucideIcon;
  /** Privilege code required to see this item. Omit for "any signed-in caller". */
  requires?: string;
  /** Match the path exactly, for an item whose path prefixes another item's. */
  end?: boolean;
}

interface NavGroup {
  label: string | null;
  items: NavItem[];
}

const NAV_GROUPS: NavGroup[] = [
  { label: null, items: [{ label: 'Home', to: paths.root, icon: House, end: true }] },
  {
    label: 'Pupils',
    items: [
      { label: 'Pupils', to: paths.pupils, icon: Users, requires: 'pupil.view' },
      { label: 'Admissions', to: paths.admissions, icon: UserPlus, requires: 'pupil.view' },
      { label: 'Incomplete records', to: paths.incompleteRecords, icon: FileWarning, requires: 'report.view' },
    ],
  },
  {
    label: 'Results',
    items: [
      { label: 'Results', to: paths.results, icon: ChartColumn, requires: 'result.view', end: true },
      { label: 'Marks', to: paths.marks, icon: PenLine, requires: 'result.view' },
      { label: 'Class records', to: paths.classRecords, icon: NotebookTabs, requires: 'result.view' },
      { label: 'Weekly reports', to: paths.weekly, icon: CalendarCheck, requires: 'weekly.view' },
      { label: 'Pins', to: paths.pins, icon: KeyRound, requires: 'pin.view' },
    ],
  },
  {
    label: 'School setup',
    items: [
      { label: 'Sessions', to: paths.sessions, icon: CalendarRange, requires: 'session.view' },
      { label: 'Classes', to: paths.classes, icon: Layers, requires: 'level.view' },
      { label: 'Arms', to: paths.arms, icon: LayoutGrid, requires: 'arm.view' },
      { label: 'Subjects', to: paths.subjects, icon: BookOpen, requires: 'subject.view' },
    ],
  },
  {
    label: 'Administration',
    items: [
      { label: 'Admins', to: paths.admins, icon: ShieldUser, requires: 'admin.view' },
      { label: 'Roles', to: paths.roles, icon: ShieldCheck, requires: 'role.view' },
      { label: 'Settings', to: paths.settings, icon: Settings, requires: 'settings.view' },
      { label: 'Audit log', to: paths.audit, icon: ScrollText, requires: 'audit.view' },
    ],
  },
];

export function AuthenticatedShell({
  session,
  children,
}: {
  session: AuthSession;
  children: ReactNode;
}) {
  const signOut = useSignOut();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const groups = NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => !item.requires || hasPrivilege(session, item.requires)),
  })).filter((group) => group.items.length > 0);

  // A drawer left open over the page it just navigated to would hide that page.
  const closeDrawer = () => setDrawerOpen(false);

  const sidebar = (
    <div className="flex h-full flex-col">
      <div className="flex h-16 shrink-0 items-center gap-3 border-b border-border pr-12 pl-4 lg:pr-4">
        <Crest />
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-foreground">{APP_NAME}</p>
          <p className="truncate text-xs text-muted-foreground">Sailing into greatness</p>
        </div>
      </div>

      <nav aria-label="Main" className="flex-1 overflow-y-auto px-3 py-4">
        {groups.map((group) => (
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
                    onClick={closeDrawer}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                        isActive
                          ? 'bg-primary/10 text-primary'
                          : 'text-muted-foreground hover:bg-muted hover:text-foreground',
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
        <Link
          to={paths.changePassword}
          onClick={closeDrawer}
          className="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
        >
          <Lock className="size-4" aria-hidden="true" />
          Change password
        </Link>
        <button
          type="button"
          onClick={() => signOut.mutate()}
          disabled={signOut.isPending}
          className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:opacity-50"
        >
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

  return (
    <div className="min-h-dvh bg-background">
      <a
        href="#main"
        className="sr-only rounded-md bg-primary px-4 py-2 text-primary-foreground focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-50"
      >
        Skip to main content
      </a>

      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 border-r border-border bg-surface lg:block">{sidebar}</aside>

      {drawerOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <button
            type="button"
            aria-label="Close menu"
            className="absolute inset-0 bg-foreground/30"
            onClick={closeDrawer}
          />
          <aside className="absolute inset-y-0 left-0 w-72 max-w-[85vw] border-r border-border bg-surface shadow-xl">
            <Button
              variant="ghost"
              size="sm"
              aria-label="Close menu"
              className="absolute top-4 right-2"
              onClick={closeDrawer}
            >
              <X className="size-4" aria-hidden="true" />
            </Button>
            {sidebar}
          </aside>
        </div>
      ) : null}

      <div className="flex min-h-dvh flex-col lg:pl-64">
        <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-border bg-surface/85 px-4 backdrop-blur sm:px-6">
          <Button
            variant="ghost"
            size="sm"
            aria-label="Open menu"
            className="lg:hidden"
            onClick={() => setDrawerOpen(true)}
          >
            <Menu className="size-5" aria-hidden="true" />
          </Button>
          <p className="truncate text-sm font-semibold text-foreground lg:hidden">{APP_NAME}</p>
          <div className="ml-auto">
            <ThemeToggle />
          </div>
        </header>

        <main id="main" className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
          {children}
        </main>
      </div>
    </div>
  );
}
