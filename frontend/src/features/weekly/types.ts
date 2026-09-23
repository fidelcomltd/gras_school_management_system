import type { components } from '@/api/schema';

type S = components['schemas'];

export type WeeklyGridDto = S['WeeklyGridDto'];
export type WeeklyGridRowDto = S['WeeklyGridRowDto'];
export type WeeklyDayDto = S['WeeklyDayDto'];
export type WeeklyDay = S['WeeklyDay'];
export type WeeklyField = S['WeeklyField'];
export type WeeklyCellInput = S['WeeklyCellInput'];

/** Mon–Fri, in the paper form's order. */
export const WEEKLY_DAYS: readonly WeeklyDay[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'];

/** The eight lines of a day panel (spec 6.10.2), in the paper form's order, with its limit (6.10.6). */
export const WEEKLY_FIELDS: readonly { field: WeeklyField; label: string; key: keyof WeeklyDayDto; max: number }[] = [
  { field: 'Behaviour', label: 'Behaviour', key: 'behaviour', max: 300 },
  { field: 'Performance', label: 'Performance', key: 'performance', max: 300 },
  { field: 'Dressing', label: 'Dressing', key: 'dressing', max: 300 },
  { field: 'HomeWork', label: 'Home Work', key: 'homeWork', max: 300 },
  { field: 'Eating', label: 'Eating', key: 'eating', max: 300 },
  { field: 'SymptomsOfIllness', label: 'Symptoms of illness', key: 'symptomsOfIllness', max: 300 },
  { field: 'TeacherComment', label: "Teacher's Comment", key: 'teacherComment', max: 500 },
  { field: 'ParentComment', label: "Parent's Comment", key: 'parentComment', max: 500 },
];

export type WeeklyFieldMeta = (typeof WEEKLY_FIELDS)[number];

/** The metadata for one line. */
export function fieldMeta(field: WeeklyField): WeeklyFieldMeta {
  const meta = WEEKLY_FIELDS.find((candidate) => candidate.field === field);
  if (!meta) throw new Error(`Unknown weekly line: ${field}`);
  return meta;
}

/** The phrase list for a field, from the grid's `phrases`. */
export const PHRASE_KEY: Record<WeeklyField, keyof S['WeeklyPhrasesDto']> = {
  Behaviour: 'behaviour',
  Performance: 'performance',
  Dressing: 'dressing',
  HomeWork: 'homeWork',
  Eating: 'eating',
  SymptomsOfIllness: 'symptomsOfIllness',
  TeacherComment: 'teacherComment',
  ParentComment: 'parentComment',
};

/** One cell's identity in the draft map. */
export const cellKey = (pupilId: string, day: WeeklyDay, field: WeeklyField) => `${pupilId}|${day}|${field}`;

/** A server value for one cell of the grid. */
export function serverValue(row: WeeklyGridRowDto, day: WeeklyDay, field: WeeklyField): string {
  const panel = row.days.find((candidate) => candidate.dayOfWeek === day);
  const meta = WEEKLY_FIELDS.find((candidate) => candidate.field === field);
  const value = panel && meta ? panel[meta.key] : null;
  return typeof value === 'string' ? value : '';
}

/** "Week 4: 12/01/2027 to 16/01/2027" (spec 6.10.3). */
export function weekLabel(week: { weekNumber: number | string; startDate: string; endDate: string }) {
  return `Week ${week.weekNumber}: ${formatDate(week.startDate)} to ${formatDate(week.endDate)}`;
}

/** An ISO date as DD/MM/YYYY. */
export function formatDate(iso: string) {
  const [year, month, day] = iso.split('-');
  return `${day ?? ''}/${month ?? ''}/${year ?? ''}`;
}
