import { useState } from 'react';
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react';
import { LoadingState, QueryErrorState, FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useSections } from '@/features/classes/api';
import { ApiError } from '@/lib/http';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useFeeNoticeGrid, useFetchFeeNoticeGrid, useSaveFeeNoticeGrid } from './api';
import { copyAmounts, draftProblem, LABEL_MAX, MAX_LINES, move, newKey, toCommand, toDraft, type DraftLine } from './fee-grid-model';
import type { FeeNoticeGridDto } from './types';

const inputClass = 'h-8 w-full rounded-md border border-input bg-surface px-2 text-sm text-foreground disabled:opacity-60';

/** Spec 6.2.13's entry grid: the section's lines against its class levels, for the term whose sheets print them. */
export function FeeGridPanel({ termId }: { termId: string }) {
  const sections = useSections();
  const [sectionChoice, setSectionChoice] = useState<string | null>(null);
  const sectionList = sections.data?.sections ?? [];
  const sectionId = sectionChoice ?? sectionList[0]?.id ?? '';
  const grid = useFeeNoticeGrid(sectionId, termId);
  // Held here, not in the editor: a save refetches the grid and remounts the editor, and the outcome must survive that.
  const save = useSaveFeeNoticeGrid();

  return (
    <div className="flex flex-col gap-4">
      <LabelledSelect
        label="Section"
        placeholder="Section"
        value={sectionId}
        options={sectionList.map((section) => ({ value: section.id, label: section.name }))}
        onChange={(id) => {
          setSectionChoice(id);
          save.reset();
        }}
        className="w-48"
      />
      {sections.isError ? <QueryErrorState error={sections.error} onRetry={() => void sections.refetch()} /> : null}
      {grid.isPending && sectionId !== '' ? <LoadingState label="Loading fee lines…" /> : null}
      {grid.isError ? <QueryErrorState error={grid.error} onRetry={() => void grid.refetch()} /> : null}
      {grid.data ? <FeeGridEditor key={`${grid.data.sectionId}:${grid.data.termId}:${grid.dataUpdatedAt}`} grid={grid.data} save={save} /> : null}
    </div>
  );
}

function FeeGridEditor({ grid, save }: { grid: FeeNoticeGridDto; save: ReturnType<typeof useSaveFeeNoticeGrid> }) {
  const [lines, setLines] = useState<DraftLine[]>(() => toDraft(grid));
  const [copyError, setCopyError] = useState<string | null>(null);
  const [copying, setCopying] = useState(false);
  const fetchGrid = useFetchFeeNoticeGrid();
  const problem = draftProblem(lines);
  const hasOutstanding = lines.some((line) => line.kind === 'Outstanding');

  const update = (key: string, change: Partial<DraftLine>) =>
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...change } : line)));
  const setCell = (key: string, levelId: string, text: string) =>
    setLines((current) => current.map((line) => (line.key === key ? { ...line, cells: { ...line.cells, [levelId]: text } } : line)));
  const addLine = (kind: DraftLine['kind']) =>
    setLines((current) => [
      ...current,
      { key: newKey(), id: null, label: kind === 'Outstanding' ? 'Outstanding Fee' : '', kind, showOnPortal: false, cells: {} },
    ]);

  const copyPrevious = async () => {
    if (!grid.previousTermId) return;
    setCopying(true);
    setCopyError(null);
    try {
      setLines(copyAmounts(lines, await fetchGrid(grid.sectionId, grid.previousTermId)));
    } catch (error) {
      setCopyError(error instanceof ApiError ? error.message : 'The previous term could not be loaded. Try again.');
    } finally {
      setCopying(false);
    }
  };

  return (
    <section aria-label={`${grid.sectionName} fee lines`} className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          Printed on {grid.sectionName} result sheets for {grid.termName} {grid.sessionName}, as next term&apos;s fees. Zero or blank
          prints as a dash.
          {grid.isDefault ? ' These are the suggested lines; nothing is saved until you save.' : ''}
        </p>
        {grid.previousTermId ? (
          <Button variant="outline" size="sm" disabled={copying} onClick={() => void copyPrevious()}>
            {copying ? 'Copying…' : `Copy from ${grid.previousTermLabel ?? 'previous term'}`}
          </Button>
        ) : null}
      </div>
      <FormError message={copyError} />

      <div className="overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-muted/50 text-xs text-muted-foreground uppercase">
            <tr>
              <th scope="col" className="px-3 py-2 font-semibold">Line</th>
              {grid.levels.map((level) => (
                <th key={level.classLevelId} scope="col" className="px-3 py-2 font-semibold">
                  {level.name}
                </th>
              ))}
              <th scope="col" className="px-3 py-2 font-semibold">
                <span className="sr-only">Order and remove</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line, index) => (
              <tr key={line.key} className="border-t border-border align-top">
                <th scope="row" className="min-w-48 px-3 py-2 font-normal">
                  <input
                    aria-label={`Label for line ${index + 1}`}
                    className={inputClass}
                    maxLength={LABEL_MAX}
                    value={line.label}
                    onChange={(event) => update(line.key, { label: event.target.value })}
                  />
                </th>
                {line.kind === 'Outstanding' ? (
                  <td colSpan={grid.levels.length} className="px-3 py-2 text-muted-foreground">
                    <span className="block">Typed per pupil, under Outstanding figures.</span>
                    <label className="mt-1 flex items-center gap-2 text-foreground">
                      <input
                        type="checkbox"
                        checked={line.showOnPortal}
                        onChange={(event) => update(line.key, { showOnPortal: event.target.checked })}
                      />
                      Show it to parents on the portal (applies to results published after you save)
                    </label>
                  </td>
                ) : (
                  grid.levels.map((level) => (
                    <td key={level.classLevelId} className="px-3 py-2">
                      <input
                        aria-label={`${line.label || `Line ${index + 1}`}, ${level.name}`}
                        inputMode="numeric"
                        className={`${inputClass} min-w-24 text-right`}
                        value={line.cells[level.classLevelId] ?? ''}
                        onChange={(event) => setCell(line.key, level.classLevelId, event.target.value)}
                      />
                    </td>
                  ))
                )}
                <td className="px-3 py-2">
                  <div className="flex gap-1">
                    <Button variant="ghost" size="sm" aria-label={`Move ${line.label || 'line'} up`} disabled={index === 0}
                      onClick={() => setLines((current) => move(current, index, -1))}>
                      <ArrowUp className="size-4" aria-hidden="true" />
                    </Button>
                    <Button variant="ghost" size="sm" aria-label={`Move ${line.label || 'line'} down`} disabled={index === lines.length - 1}
                      onClick={() => setLines((current) => move(current, index, 1))}>
                      <ArrowDown className="size-4" aria-hidden="true" />
                    </Button>
                    <Button variant="ghost" size="sm" aria-label={`Remove ${line.label || 'line'}`}
                      onClick={() => setLines((current) => current.filter((candidate) => candidate.key !== line.key))}>
                      <Trash2 className="size-4" aria-hidden="true" />
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={lines.length >= MAX_LINES} onClick={() => addLine('Amount')}>
            <Plus className="size-4" aria-hidden="true" /> Add line
          </Button>
          {hasOutstanding ? null : (
            <Button variant="outline" size="sm" disabled={lines.length >= MAX_LINES} onClick={() => addLine('Outstanding')}>
              <Plus className="size-4" aria-hidden="true" /> Add outstanding-fee line
            </Button>
          )}
        </div>
        <div className="flex items-center gap-3">
          {problem ? <span className="text-sm text-destructive">{problem}</span> : null}
          <Button disabled={problem !== null || save.isPending} onClick={() => save.mutate(toCommand(lines, grid))}>
            {save.isPending ? 'Saving…' : 'Save fee lines'}
          </Button>
        </div>
      </div>
      {lines.length === 0 ? (
        <p className="text-sm text-muted-foreground">With no lines, {grid.sectionName} result sheets print no fees block.</p>
      ) : null}
      <FormError message={save.error instanceof ApiError ? save.error.message : null} />
      {save.isSuccess ? <output className="text-sm text-success">Saved.</output> : null}
      <p className="text-xs text-muted-foreground">
        Removing a line removes its amounts in every term. Sheets already published keep the lines they were printed with.
      </p>
    </section>
  );
}
