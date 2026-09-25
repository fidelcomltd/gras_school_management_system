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
  NotebookTabs,
  PenLine,
  ScrollText,
  Settings,
  ShieldCheck,
  ShieldUser,
  UserPlus,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { paths } from '@/app/router/paths';
import { hasPrivilege, type AuthSession } from '@/lib/auth/auth-session';

/**
 * The sidebar's contents. Add a feature's entry here as its route lands, gated by the same privilege as its route.
 */
export interface NavItem {
  label: string;
  to: string;
  icon: LucideIcon;
  /** Privilege code required to see this item. Omit for "any signed-in caller". */
  requires?: string;
  /** Match the path exactly, for an item whose path prefixes another item's. */
  end?: boolean;
}

export interface NavGroup {
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

/**
 * The groups this caller may see: an item the caller cannot use is ABSENT, never rendered disabled (TASK-0041 AC-2),
 * and a group left empty is dropped.
 */
export function visibleNavGroups(session: AuthSession): NavGroup[] {
  return NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => !item.requires || hasPrivilege(session, item.requires)),
  })).filter((group) => group.items.length > 0);
}
