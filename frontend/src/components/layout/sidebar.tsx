import { PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { NavLink } from 'react-router';
import type { AuthSession } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { Brand } from './app-shell';
import { visibleNavGroups } from './nav-config';

/**
 * The sidebar body, shared by the fixed desktop column and the mobile drawer. `collapsed` (desktop only) shows the icons
 * alone: each label stays in the accessible name and appears as a tooltip, so nothing is lost but width.
 */
export function Sidebar({
  session,
  onNavigate,
  collapsed = false,
  onToggleCollapsed,
}: {
  session: AuthSession;
  onNavigate?: () => void;
  collapsed?: boolean;
  onToggleCollapsed?: () => void;
}) {
  const ToggleIcon = collapsed ? PanelLeftOpen : PanelLeftClose;

  return (
    <div className="flex h-full flex-col">
      <div className={cn('flex h-16 shrink-0 items-center border-b border-border', collapsed ? 'justify-center px-2' : 'pr-12 pl-4 lg:pr-2')}>
        {collapsed ? null : <Brand />}
        {onToggleCollapsed ? (
          <button
            type="button"
            onClick={onToggleCollapsed}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            aria-expanded={!collapsed}
            className={cn(
              'inline-flex size-8 shrink-0 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground',
              collapsed ? '' : 'ml-auto',
            )}
          >
            <ToggleIcon className="size-4" aria-hidden="true" />
          </button>
        ) : null}
      </div>

      <nav aria-label="Main" className={cn('flex-1 overflow-y-auto py-4', collapsed ? 'px-2' : 'px-3')}>
        {visibleNavGroups(session).map((group) => (
          <div key={group.label ?? 'top'} className="mb-5 last:mb-0">
            {group.label ? (
              collapsed ? (
                <hr className="mx-2 mb-2 border-border" />
              ) : (
                <p className="mb-1 px-3 text-[11px] font-semibold tracking-wider text-muted-foreground uppercase">{group.label}</p>
              )
            ) : null}
            <ul className="flex flex-col gap-0.5">
              {group.items.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.end ?? false}
                    onClick={onNavigate}
                    title={collapsed ? item.label : undefined}
                    className={({ isActive }) =>
                      cn(
                        'flex items-center gap-3 rounded-md py-2 text-sm font-medium transition-colors',
                        collapsed ? 'justify-center px-2' : 'px-3',
                        isActive ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                      )
                    }
                  >
                    <item.icon className="size-4 shrink-0" aria-hidden="true" />
                    <span className={collapsed ? 'sr-only' : undefined}>{item.label}</span>
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>
    </div>
  );
}
