import type { FeeLabelKind, FeeNoticeGridDto, SaveFeeNoticeGridCommand } from './types';

/** One editable row. `key` is stable across edits; `id` is null until the line is saved. Cells are the typed text. */
export interface DraftLine {
  key: string;
  id: string | null;
  label: string;
  kind: FeeLabelKind;
  showOnPortal: boolean;
  cells: Record<string, string>;
}

export const MAX_AMOUNT = 100_000_000;
export const MAX_LINES = 20;
export const LABEL_MAX = 60;

let nextKey = 0;
export const newKey = () => `line-${(nextKey += 1)}`;

export function toDraft(grid: FeeNoticeGridDto): DraftLine[] {
  return grid.lines.map((line) => ({
    key: newKey(),
    id: line.id ?? null,
    label: line.label,
    kind: line.kind,
    showOnPortal: line.showOnPortal,
    cells: Object.fromEntries(line.amounts.map((amount) => [amount.classLevelId, String(amount.amount)])),
  }));
}

/** Naira as typed: digits with optional thousands separators. Blank is "no amount", and prints as a dash. */
export function parseAmount(text: string): number | null | 'invalid' {
  const cleaned = text.replaceAll(',', '').trim();
  if (cleaned === '') return null;
  if (!/^\d+$/.test(cleaned)) return 'invalid';
  const value = Number(cleaned);
  return value > MAX_AMOUNT ? 'invalid' : value;
}

/** What stops the save: a blank or long label, a duplicate label, or a cell that is not a whole naira amount. */
export function draftProblem(lines: DraftLine[]): string | null {
  if (lines.length === 0) return 'Add at least one line.';
  if (lines.length > MAX_LINES) return `A fee notice can have at most ${MAX_LINES} lines.`;
  const labels = lines.map((line) => line.label.trim().toUpperCase());
  if (labels.some((label) => label.length === 0 || label.length > LABEL_MAX)) return `Every line needs a label of 1 to ${LABEL_MAX} characters.`;
  if (new Set(labels).size !== labels.length) return 'Two lines have the same label.';
  const badCell = lines.some((line) => Object.values(line.cells).some((cell) => parseAmount(cell) === 'invalid'));
  return badCell ? `Amounts are whole naira, from 0 to ${MAX_AMOUNT.toLocaleString('en-NG')}.` : null;
}

/** The whole grid as one save; every level is sent for every amount line, so a cleared cell is cleared. */
export function toCommand(lines: DraftLine[], grid: FeeNoticeGridDto): SaveFeeNoticeGridCommand {
  return {
    sectionId: grid.sectionId,
    termId: grid.termId,
    lines: lines.map((line) => ({
      id: line.id,
      label: line.label.trim(),
      kind: line.kind,
      showOnPortal: line.kind === 'Outstanding' && line.showOnPortal,
      amounts:
        line.kind === 'Outstanding'
          ? []
          : grid.levels.map((level) => {
              const parsed = parseAmount(line.cells[level.classLevelId] ?? '');
              return { classLevelId: level.classLevelId, amount: parsed === 'invalid' ? null : parsed };
            }),
    })),
  };
}

/**
 * "Copy from previous term": fills each amount line's cells from the earlier term's line of the same id or, failing that,
 * the same label. Lines the earlier term lacks keep what they have; nothing is saved until the admin saves.
 */
export function copyAmounts(lines: DraftLine[], previous: FeeNoticeGridDto): DraftLine[] {
  return lines.map((line) => {
    if (line.kind === 'Outstanding') return line;
    const source =
      previous.lines.find((candidate) => line.id !== null && candidate.id === line.id) ??
      previous.lines.find((candidate) => candidate.label.trim().toUpperCase() === line.label.trim().toUpperCase());
    if (!source) return line;
    return { ...line, cells: Object.fromEntries(source.amounts.map((amount) => [amount.classLevelId, String(amount.amount)])) };
  });
}

/** A line's position swapped with its neighbour's. */
export function move(lines: DraftLine[], index: number, by: -1 | 1): DraftLine[] {
  const target = index + by;
  const current = lines[index];
  const neighbour = lines[target];
  if (!current || !neighbour) return lines;
  const next = [...lines];
  next[index] = neighbour;
  next[target] = current;
  return next;
}
