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
  | 'fee-notice-audit'
  | 'annual-cumulative'
  | 'promotion-list'
  | 'pupil-record'
  | 'nominal-roll'
  | 'enrolment-summary'
  | 'guardian-contacts'
  | 'outstanding-documents'
  | 'admissions-pipeline'
  | 'pin-usage'
  | 'audit'
  | 'settings-history';

/**
 * One filter control on a report's screen. The period is a term or a session; a class is required (`arm`); a class or a
 * whole level is required (`scope`) or optional with "whole school" (`scopeAll`); a level is required (`level`) or optional
 * (`levelAll`); the rest are optional refinements. `pupil` comes from the address, set by the pupil's own page.
 */
export type ReportControl =
  | 'term'
  | 'session'
  | 'arm'
  | 'scope'
  | 'scopeAll'
  | 'top'
  | 'level'
  | 'levelAll'
  | 'state'
  | 'outcome'
  | 'status'
  | 'sex'
  | 'documentType'
  | 'minDays'
  | 'pinState'
  | 'auditOutcome'
  | 'group'
  | 'dateRange'
  | 'pupil';

export interface TableReport {
  kind: 'table';
  key: ReportKey;
  title: string;
  description: string;
  group: string;
  privilege: string;
  controls: ReportControl[];
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

const table = (report: Omit<TableReport, 'kind' | 'privilege'> & { privilege?: string }): TableReport => ({
  kind: 'table',
  privilege: 'report.view',
  ...report,
});

/** Spec 15 section 10, in the order the hub lists them. */
export const REPORTS: ReportDefinition[] = [
  table({
    key: 'broadsheet',
    title: 'Arm broadsheet',
    description: 'Every pupil in a class with each subject’s CA, exam and total, then average and positions.',
    group: 'Results',
    controls: ['term', 'arm'],
  }),
  table({
    key: 'merit-list',
    title: 'Merit list',
    description: 'Position order for a class or a whole level, for prize-giving.',
    group: 'Results',
    controls: ['term', 'scope', 'top'],
  }),
  table({
    key: 'result-entry-progress',
    title: 'Result entry progress',
    description: 'Every class’s marks, ratings, attendance and remarks, complete of total, and where each set stands.',
    group: 'Results',
    controls: ['term', 'levelAll', 'state'],
  }),
  table({
    key: 'grade-distribution',
    title: 'Grade distribution',
    description: 'How many pupils earned each grade, per subject and overall, for a class or a level.',
    group: 'Results',
    controls: ['term', 'scope'],
  }),
  table({
    key: 'subject-performance',
    title: 'Subject performance',
    description: 'Each subject’s average, range and pass rate, class by class across a level.',
    group: 'Results',
    controls: ['term', 'level'],
  }),
  table({
    key: 'development-summary',
    title: 'Development domain summary',
    description: 'Nursery: how many children sit at each rating point of each indicator.',
    group: 'Results',
    controls: ['term', 'arm'],
  }),
  table({
    key: 'annual-cumulative',
    title: 'Annual cumulative report',
    description: 'Each pupil’s three term averages, cumulative average, annual position and promotion status.',
    group: 'Results',
    controls: ['session', 'scope'],
  }),
  table({
    key: 'promotion-list',
    title: 'Promotion list',
    description: 'Proposed and final promotion outcomes with core subject results and target classes. The document the school files.',
    group: 'Results',
    controls: ['session', 'levelAll', 'outcome'],
  }),
  table({
    key: 'nominal-roll',
    title: 'Nominal roll',
    description: 'The register by class: number, name, sex, age, admission date, status and primary guardian.',
    group: 'Pupils',
    controls: ['session', 'scopeAll', 'status', 'sex'],
  }),
  table({
    key: 'enrolment-summary',
    title: 'Enrolment summary',
    description: 'Boys, girls, capacity and space left, class by class, level by level.',
    group: 'Pupils',
    controls: ['session', 'status'],
  }),
  table({
    key: 'guardian-contacts',
    title: 'Guardian contact list',
    description: 'One class’s primary guardians and phone numbers, for calling parents. Nothing more.',
    group: 'Pupils',
    privilege: 'contact.view',
    controls: ['session', 'arm'],
  }),
  table({
    key: 'outstanding-documents',
    title: 'Outstanding admission documents',
    description: 'Every document still to collect, oldest record first.',
    group: 'Pupils',
    controls: ['session', 'scopeAll', 'documentType'],
  }),
  table({
    key: 'admissions-pipeline',
    title: 'Admissions pipeline',
    description: 'Pending admissions by level, how long each has waited and what blocks approval.',
    group: 'Pupils',
    controls: ['levelAll', 'minDays'],
  }),
  table({
    key: 'pupil-record',
    title: 'Pupil cumulative record',
    description: 'One pupil across every session, term by term. Open it from the pupil’s own page.',
    group: 'Pupils',
    controls: ['pupil'],
  }),
  table({
    key: 'pin-usage',
    title: 'Pin distribution and usage',
    description: 'How many pupils’ results were opened, class by class, and each batch’s pins used, exhausted and revoked.',
    group: 'Pins and records',
    privilege: 'pin.usage.view',
    controls: ['session', 'pinState'],
  }),
  table({
    key: 'audit',
    title: 'Audit report',
    description: 'The audit log for reading and printing, with before and after values for score changes.',
    group: 'Pins and records',
    privilege: 'audit.view',
    controls: ['dateRange', 'auditOutcome'],
  }),
  table({
    key: 'settings-history',
    title: 'Settings change history',
    description: 'Every settings change, who made it and why, and what it changed, in plain words.',
    group: 'Pins and records',
    privilege: 'audit.view',
    controls: ['dateRange', 'group'],
  }),
  table({
    key: 'fee-notice-audit',
    title: 'Fee notice audit',
    description: 'The fee lines each level’s sheets print, and the outstanding figures typed against them.',
    group: 'Fees',
    controls: ['term', 'levelAll'],
  }),
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
