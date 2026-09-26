import { Dialog as BaseDialog } from '@base-ui/react/dialog';
import { Menu, X } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { ThemeToggle } from '@/components/theme/theme-toggle';
import { Button } from '@/components/ui/button';
import { APP_NAME } from '@/config/env-values';
import type { AuthSession } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { useSidebarStore } from '@/stores/sidebar-store';
import { SkipLink } from './app-shell';
import { GlobalSearch } from './global-search';
import { Sidebar } from './sidebar';
import { UserMenu } from './user-menu';

/**
 * The back office's frame for every signed-in route with navigation (TASK-0041): a fixed sidebar on wide screens, the
 * same sidebar in a drawer below `lg` (collapsible to icons on desktop), and a top bar with the app-wide search, the
 * signed-in account and the theme toggle. The drawer is a Base UI dialog, so focus is
 * trapped inside it, `Escape` closes it and focus returns to the menu button.
 */
export function AuthenticatedShell({
  session,
  children,
}: {
  session: AuthSession;
  children: ReactNode;
}) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const collapsed = useSidebarStore((state) => state.collapsed);
  const toggleCollapsed = useSidebarStore((state) => state.toggle);

  return (
    <div className="min-h-dvh bg-background">
      <SkipLink />

      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-30 hidden border-r border-border bg-surface transition-[width] duration-200 lg:block',
          collapsed ? 'w-16' : 'w-64',
        )}
      >
        <Sidebar session={session} collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
      </aside>

      <BaseDialog.Root open={drawerOpen} onOpenChange={setDrawerOpen}>
        <BaseDialog.Portal>
          <BaseDialog.Backdrop className="fixed inset-0 z-50 bg-overlay backdrop-blur-[2px] lg:hidden" />
          <BaseDialog.Popup className="fixed inset-y-0 left-0 z-50 w-72 max-w-[85vw] border-r border-border bg-surface shadow-xl lg:hidden">
            <BaseDialog.Title className="sr-only">Menu</BaseDialog.Title>
            <BaseDialog.Close
              aria-label="Close menu"
              className="absolute top-4 right-2 inline-flex size-8 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <X className="size-4" aria-hidden="true" />
            </BaseDialog.Close>
            {/* A drawer left open over the page it just navigated to would hide that page. */}
            <Sidebar session={session} onNavigate={() => setDrawerOpen(false)} />
          </BaseDialog.Popup>
        </BaseDialog.Portal>
      </BaseDialog.Root>

      <div className={cn('flex min-h-dvh flex-col transition-[padding] duration-200', collapsed ? 'lg:pl-16' : 'lg:pl-64')}>
        <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-border bg-surface/85 px-4 backdrop-blur sm:px-6">
          <Button variant="ghost" size="sm" aria-label="Open menu" className="lg:hidden" onClick={() => setDrawerOpen(true)}>
            <Menu className="size-5" aria-hidden="true" />
          </Button>
          <p className="hidden truncate text-sm font-semibold text-foreground sm:block lg:hidden">{APP_NAME}</p>
          <GlobalSearch session={session} />
          <div className="ml-auto flex items-center gap-1">
            <ThemeToggle />
            <UserMenu session={session} />
          </div>
        </header>

        <main id="main" className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
          {children}
        </main>
      </div>
    </div>
  );
}
