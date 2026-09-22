import type { SaveScoreSheetCommand, ScoreSheetDto } from '../types';

/** What the admin has typed for one pupil; strings so a half-typed cell is representable. */
export interface RowDraft {
  marks: Record<string, string>;
  exam: string;
  absent: boolean;
}

export type SheetDraft = Record<string, RowDraft>;

export function draftFrom(sheet: ScoreSheetDto): SheetDraft {
  return Object.fromEntries(
    sheet.rows.map((row) => [
      row.pupilId,
      {
        marks: Object.fromEntries(sheet.components.map((c) => [c.id, row.componentMarks[c.id]?.toString() ?? ''])),
        exam: row.examMark?.toString() ?? '',
        absent: row.examAbsent,
      },
    ]),
  );
}

/** A cell is blank, or a whole number from 0 to its maximum (spec 6.7.4). */
export function cellError(value: string, max: number): string | null {
  if (value.trim() === '') return null;
  if (!/^\d+$/.test(value.trim())) return 'Whole numbers only';
  return Number(value) > max ? `At most ${max}` : null;
}

const toMark = (value: string) => (value.trim() === '' ? null : Number(value));

/** The live total shown while typing; the stored total comes back from the server on save. */
export function rowTotal(row: RowDraft): number | null {
  const parts = [...Object.values(row.marks), row.absent ? '' : row.exam].map(toMark).filter((m): m is number => m !== null);
  return parts.length === 0 ? null : parts.reduce((sum, mark) => sum + mark, 0);
}

export function hasErrors(sheet: ScoreSheetDto, draft: SheetDraft): boolean {
  return Object.values(draft).some(
    (row) =>
      sheet.components.some((c) => cellError(row.marks[c.id] ?? '', Number(c.maxMark)) !== null) ||
      (!row.absent && cellError(row.exam, Number(sheet.examination.maxMark)) !== null),
  );
}

export function toCommand(sheet: ScoreSheetDto, draft: SheetDraft): SaveScoreSheetCommand {
  return {
    armId: sheet.armId,
    subjectId: sheet.subjectId,
    termId: sheet.termId,
    version: sheet.version ?? null,
    rows: sheet.rows.map((row) => {
      const entry = draft[row.pupilId] ?? { marks: {}, exam: '', absent: false };
      return {
        pupilId: row.pupilId,
        componentMarks: Object.fromEntries(sheet.components.map((c) => [c.id, toMark(entry.marks[c.id] ?? '')])),
        examMark: entry.absent ? null : toMark(entry.exam),
        examAbsent: entry.absent,
      };
    }),
  };
}
