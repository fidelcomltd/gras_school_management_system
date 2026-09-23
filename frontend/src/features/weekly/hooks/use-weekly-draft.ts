import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/http';
import { useSaveWeeklyNotes } from '../api';
import { WEEKLY_FIELDS, cellKey, serverValue, type WeeklyCellInput, type WeeklyDay, type WeeklyField, type WeeklyGridDto } from '../types';

/** Spec 6.10.7: autosave every thirty seconds, and on cell blur. */
export const AUTOSAVE_MS = 30_000;

export interface WeeklyDraft {
  value: (pupilId: string, day: WeeklyDay, field: WeeklyField) => string;
  set: (pupilId: string, day: WeeklyDay, field: WeeklyField, value: string) => void;
  /** Sets many cells at once (Fill across, Fill down). */
  setMany: (cells: { pupilId: string; day: WeeklyDay; field: WeeklyField; value: string }[]) => void;
  isTooLong: (pupilId: string, day: WeeklyDay, field: WeeklyField) => boolean;
  flush: () => void;
  /** Clears a stalled save and tries again now. */
  retry: () => void;
  /** Saves after the next render, once a just-made change has landed (Fill down). */
  flushSoon: () => void;
  unsaved: number;
  saving: boolean;
  /** The last save failure; the edits are kept (spec 9.8.2). */
  error: string | null;
  /** True when retrying cannot help (a rejection, not a dropped connection): autosave waits for the next edit. */
  stalled: boolean;
}

const MAX_BY_FIELD = Object.fromEntries(WEEKLY_FIELDS.map((meta) => [meta.field, meta.max])) as Record<WeeklyField, number>;

/** A 4xx other than a conflict or throttling fails the same way again; a 409 (a concurrent first note) or a network fault may not. */
const isPermanent = (failure: unknown) =>
  failure instanceof ApiError &&
  failure.status !== undefined &&
  failure.status >= 400 &&
  failure.status < 500 &&
  failure.status !== 409 &&
  failure.status !== 429;

/**
 * Unsaved cells held on top of one week's grid. Saves are sparse — only the cells touched are sent — and on success the
 * saved values are written into the cached grid, so a refetch never races a teacher mid-sentence. Saves are promise-based,
 * so the final save when the week is left still lands in the cache after the editor has unmounted, and an edit made while
 * a save is in flight is sent as soon as it returns.
 */
export function useWeeklyDraft(grid: WeeklyGridDto, queryKey: readonly unknown[]): WeeklyDraft {
  const queryClient = useQueryClient();
  const { mutateAsync, isPending } = useSaveWeeklyNotes(grid.armId);
  const queryKeyRef = useRef(queryKey);
  const [edits, setEdits] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [stalled, setStalled] = useState(false);
  const editsRef = useRef(edits);
  const stalledRef = useRef(false);
  // Synced after each render; declared before the effects that flush, so they always read the committed edits.
  useEffect(() => {
    queryKeyRef.current = queryKey;
    editsRef.current = edits;
    stalledRef.current = stalled;
  });
  const savingRef = useRef(false);
  const againRef = useRef(false);
  const rowsById = new Map(grid.rows.map((row) => [row.pupilId, row]));

  const original = (pupilId: string, day: WeeklyDay, field: WeeklyField) => {
    const row = rowsById.get(pupilId);
    return row ? serverValue(row, day, field) : '';
  };

  const value = (pupilId: string, day: WeeklyDay, field: WeeklyField) => edits[cellKey(pupilId, day, field)] ?? original(pupilId, day, field);

  const setMany: WeeklyDraft['setMany'] = (cells) => {
    setStalled(false);
    setEdits((current) => {
      const next = { ...current };
      for (const cell of cells) {
        const key = cellKey(cell.pupilId, cell.day, cell.field);
        if (cell.value === original(cell.pupilId, cell.day, cell.field)) delete next[key];
        else next[key] = cell.value;
      }
      return next;
    });
  };

  const isTooLong = (pupilId: string, day: WeeklyDay, field: WeeklyField) => value(pupilId, day, field).trim().length > MAX_BY_FIELD[field];

  const termId = grid.termId;
  const weekNumber = Number(grid.weekNumber);
  const flushRef = useRef<() => void>(() => undefined);
  const flush = useCallback(() => {
    if (savingRef.current) {
      againRef.current = true; // Sent as soon as the save in flight returns.
      return;
    }
    if (stalledRef.current) return;
    const snapshot = Object.entries(editsRef.current).filter(([key, text]) => {
      const field = key.split('|')[2] as WeeklyField;
      return text.trim().length <= MAX_BY_FIELD[field];
    });
    if (snapshot.length === 0) return;

    const cells: WeeklyCellInput[] = snapshot.map(([key, text]) => {
      const [pupilId = '', dayOfWeek, field] = key.split('|') as [string, WeeklyDay, WeeklyField];
      return { pupilId, dayOfWeek, field, value: text.trim() === '' ? null : text };
    });

    savingRef.current = true;
    const save = async () => {
      try {
        await mutateAsync({ termId, weekNumber, cells });
        setError(null);
        queryClient.setQueryData<WeeklyGridDto>(queryKeyRef.current, (cached) => (cached ? applyCells(cached, cells) : cached));
        // Only drop an edit if it has not been typed over while the save was in flight.
        const remaining = { ...editsRef.current };
        for (const [key, text] of snapshot) if (remaining[key] === text) delete remaining[key];
        editsRef.current = remaining;
        setEdits(remaining);
      } catch (failure: unknown) {
        setError(failure instanceof ApiError ? failure.message : 'Could not save. Retrying shortly.');
        stalledRef.current = isPermanent(failure);
        setStalled(stalledRef.current);
      } finally {
        savingRef.current = false;
        if (againRef.current) {
          againRef.current = false;
          flushRef.current();
        }
      }
    };
    void save();
  }, [mutateAsync, termId, weekNumber, queryClient]);
  useEffect(() => {
    flushRef.current = flush;
  }, [flush]);

  // Every thirty seconds; and once more when the week is left, so switching weeks never drops a note.
  useEffect(() => {
    const timer = setInterval(flush, AUTOSAVE_MS);
    return () => {
      clearInterval(timer);
      flush();
    };
  }, [flush]);

  const [flushRequest, setFlushRequest] = useState(0);
  useEffect(() => {
    if (flushRequest > 0) flush();
  }, [flushRequest, flush]);

  const unsaved = Object.keys(edits).length;
  useEffect(() => {
    if (unsaved === 0) return;
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [unsaved]);

  return {
    value,
    set: (pupilId, day, field, text) => setMany([{ pupilId, day, field, value: text }]),
    setMany,
    isTooLong,
    flush,
    retry: () => {
      stalledRef.current = false;
      setStalled(false);
      flush();
    },
    flushSoon: () => setFlushRequest((count) => count + 1),
    unsaved,
    saving: isPending,
    error,
    stalled,
  };
}

/** The cached grid with the saved cells written in. */
function applyCells(grid: WeeklyGridDto, cells: WeeklyCellInput[]): WeeklyGridDto {
  return {
    ...grid,
    rows: grid.rows.map((row) => {
      const mine = cells.filter((cell) => cell.pupilId === row.pupilId);
      if (mine.length === 0) return row;
      return {
        ...row,
        days: row.days.map((day) => {
          const updated = { ...day };
          for (const cell of mine.filter((candidate) => candidate.dayOfWeek === day.dayOfWeek)) {
            const meta = WEEKLY_FIELDS.find((candidate) => candidate.field === cell.field);
            if (meta) Object.assign(updated, { [meta.key]: cell.value?.trim() || null });
          }
          return updated;
        }),
      };
    }),
  };
}
