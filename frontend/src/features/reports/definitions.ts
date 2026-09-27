import type { components } from '@/api/schema';
import { paths } from '@/app/router/paths';

export type ReportDto = components['schemas']['ReportDto'];
export type ReportColumnDto = components['schemas']['ReportColumnDto'];
export type ReportRowDto = components['schemas']['ReportRowDto'];

/** The reports served by the shared table shape, each with its own filters. */
export type ReportKey =
  | 'broadsheet'
  | 'merit-list'
  | 'result-entry-progress'
  | 'grade-distribution'
  | 'subject-performance'
  | 'development-summary'
  | 'fee-notice-audit';

/**
 * Which filter set a report's screen shows: a class; a class or a whole level (with a top N for the merit list); a required
 * level; an optional level (with a result-set state for progress).
 */
export type ReportFilterKind =
  | 'term-arm'
  | 'term-scope'
  | 'term-scope-top'
  | 'term-level'
  | 'term-level-state'
  | 'term-level-any';

export interface TableReport {
  kind: 'table';
  key: ReportKey;
  title: string;
  description: string;
  group: string;
  privilege: string;
  filters: ReportFilterKind;
}

/** A report with a screen of its own, listed here so the hub shows every report in one place. */
export interface LinkedReport {
  kind: 'link';
  title: string;
  description: string;
  group: string;
  privilege: string;
  to: string;
}

export type ReportDefinition = TableReport | LinkedReport;

/** Spec 15 section 10, in the order the hub lists them. */
export const REPORTS: ReportDefinition[] = [
  {
    kind: 'table',
    key: 'broadsheet',
    title: 'Arm broadsheet',
    description: 'Every pupil in a class with each subject’s CA, exam and total, then average and positions.',
    group: 'Results',
    privilege: 'report.view',
    filters: 'term-arm',
  },
  {
    kind: 'table',
    key: 'merit-list',
    title: 'Merit list',
    description: 'Position order for a class or a whole level, for prize-giving.',
    group: 'Results',
    privilege: 'report.view',
    filters: 'term-scope-top',
  },
  {
    kind: 'table',
    key: 'result-entry-progress',
    title: 'Result entry progress',
    description: 'Every class’s marks, ratings, attendance and remarks, complete of total, and where each set stands.',
    group: 'Results',
    privilege: 'report.view',
    filters: 'term-level-state',
  },
  {
    kind: 'table',
    key: 'grade-distribution',
    title: 'Grade distribution',
    description: 'How many pupils earned each grade, per subject and overall, for a class or a level.',
    group: 'Results',
    privilege: 'report.view',
    filters: 'term-scope',
  },
  {
    kind: 'table',
    key: 'subject-performance',
    title: 'Subject performance',
    description: 'Each subject’s average, range and pass rate, class by class across a level.',
    group: 'Results',
    privilege: 'report.view',
    filters: 'term-level',
  },
  {
    kind: 'table',
    key: 'development-summary',
    title: 'Development domain summary',
    description: 'Nursery: how many children sit at each rating point of each indicator.',
    group: 'Results',
    privilege: 'report.view',
    filters: 'term-arm',
  },
  {
    kind: 'table',
    key: 'fee-notice-audit',
    title: 'Fee notice audit',
    description: 'The fee lines each level’s sheets print, and the outstanding figures typed against them.',
    group: 'Fees',
    privilege: 'report.view',
    filters: 'term-level-any',
  },
  {
    kind: 'link',
    title: 'Weekly report completion',
    description: 'Which classes wrote their weekly notes, week by week.',
    group: 'Weekly reports',
    privilege: 'weekly.view',
    to: paths.weeklyCompletion,
  },
  {
    kind: 'link',
    title: 'Incomplete pupil records',
    description: 'Active pupils with something still missing from their record.',
    group: 'Pupils',
    privilege: 'report.view',
    to: paths.incompleteRecords,
  },
  {
    kind: 'link',
    title: 'Class safeguarding sheet',
    description: 'Allergies, medication, hospital and pickup persons for one class. Audited.',
    group: 'Pupils',
    privilege: 'pupil.safeguarding.view',
    to: paths.safeguardingSheet,
  },
];

export function tableReport(key: string | undefined): TableReport | undefined {
  return REPORTS.find((report): report is TableReport => report.kind === 'table' && report.key === key);
}
