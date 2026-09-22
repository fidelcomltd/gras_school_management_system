import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { useCopyMappings, useMappingGrid, usePrefillMappings, useSaveMappingGrid } from './api';
import { ChangePreview } from './components/change-preview';
import { CopyFromTerm } from './components/copy-from-term';
import { MappingGrid } from './components/mapping-grid';
import { cellKey, type SaveSubjectMappingGridResponse, type SubjectMappingGridDto, type SubjectMappingGridEntryInput } from './types';

type Pending = { kind: 'grid' } | { kind: 'copy'; sourceTermId: string } | { kind: 'prefill' };

/** Every mapped cell after the admin's edits; a newly ticked cell goes to the end of its class's order. */
function entriesFor(grid: SubjectMappingGridDto, edits: ReadonlyMap<string, boolean>): SubjectMappingGridEntryInput[] {
  const next = new Map<string, number>();
  for (const level of grid.levels) {
    const orders = grid.subjects.flatMap((s) => s.cells.filter((c) => c.classLevelId === level.classLevelId && c.mapped).map((c) => Number(c.displayOrder ?? 0)));
    next.set(level.classLevelId, Math.max(0, ...orders) + 1);
  }

  return grid.subjects.flatMap((subject) =>
    grid.levels.flatMap((level) => {
      const cell = subject.cells.find((c) => c.classLevelId === level.classLevelId);
      if (!(edits.get(cellKey(subject.subjectId, level.classLevelId)) ?? cell?.mapped ?? false)) return [];
      let displayOrder = cell?.mapped && cell.displayOrder != null ? Number(cell.displayOrder) : null;
      if (displayOrder === null) {
        displayOrder = next.get(level.classLevelId) ?? 1;
        next.set(level.classLevelId, displayOrder + 1);
      }
      return [{ subjectId: subject.subjectId, classLevelId: level.classLevelId, displayOrder }];
    }),
  );
}

/** `/subjects/mapping` — which classes take which subject in a term (spec 6.6.5–6.6.9), always previewed before saving. */
export function MappingScreen() {
  const choice = useTermChoice();
  const grid = useMappingGrid(choice.termId);
  const save = useSaveMappingGrid(choice.termId);
  const copy = useCopyMappings(choice.termId);
  const prefill = usePrefillMappings(choice.termId);
  const me = useMe();
  const [edits, setEdits] = useState<Map<string, boolean>>(new Map());
  const [pending, setPending] = useState<Pending | null>(null);
  const [preview, setPreview] = useState<SaveSubjectMappingGridResponse | null>(null);
  const canMap = !!me.data && (hasPrivilege(me.data, 'subject.map') || hasPrivilege(me.data, 'subject.unmap'));
  const closed = choice.term?.state === 'Closed';
  const busy = save.isPending || copy.isPending || prefill.isPending;
  const error = [save.error, copy.error, prefill.error].find((e) => e instanceof ApiError);

  const run = (operation: Pending, dryRun: boolean) => {
    const done = {
      onSuccess: (result: SaveSubjectMappingGridResponse) => {
        if (dryRun) {
          setPending(operation);
          setPreview(result);
        } else {
          setPending(null);
          setPreview(null);
          setEdits(new Map());
        }
      },
    };
    if (operation.kind === 'grid' && grid.data) save.mutate({ entries: entriesFor(grid.data, edits), dryRun }, done);
    if (operation.kind === 'copy') copy.mutate({ sourceTermId: operation.sourceTermId, dryRun }, done);
    if (operation.kind === 'prefill') prefill.mutate(dryRun, done);
  };

  return (
    <div className="flex flex-col gap-6">
      <Link to={paths.subjects} className="text-sm text-primary hover:underline">
        ← Subjects
      </Link>
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Subjects by class</h1>
        <p className="text-sm text-muted-foreground">Tick the subjects each class takes this term. Changes are previewed before they are saved.</p>
      </header>

      <TermPicker choice={choice} />
      {closed ? <p className="text-sm text-muted-foreground">This term is closed; its subjects can no longer change.</p> : null}
      <FormError message={error?.message} />

      {choice.isPending || (choice.termId && grid.isPending) ? (
        <LoadingState label="Loading subjects…" />
      ) : !choice.termId ? (
        <p className="text-sm text-muted-foreground">Create a session first; subjects are mapped per term.</p>
      ) : grid.isError ? (
        <QueryErrorState error={grid.error} onRetry={() => void grid.refetch()} />
      ) : grid.data ? (
        grid.data.subjects.length === 0 || grid.data.levels.length === 0 ? (
          <p className="text-sm text-muted-foreground">Add subjects and classes first.</p>
        ) : (
          <>
            {preview && pending ? (
              <ChangePreview preview={preview} applying={busy} onApply={() => run(pending, false)} onCancel={() => setPreview(null)} />
            ) : null}
            {canMap && !closed ? (
              <div className="flex flex-wrap gap-3">
                <Button disabled={busy || edits.size === 0} onClick={() => run({ kind: 'grid' }, true)}>
                  Review changes
                </Button>
                <Button variant="ghost" disabled={edits.size === 0} onClick={() => setEdits(new Map())}>
                  Discard edits
                </Button>
                <Button variant="outline" disabled={busy} onClick={() => run({ kind: 'prefill' }, true)}>
                  Add the standard subject list
                </Button>
              </div>
            ) : null}
            <MappingGrid
              grid={grid.data}
              edits={edits}
              readOnly={!canMap || closed}
              onToggle={(key, mapped) => setEdits((current) => new Map(current).set(key, mapped))}
            />
            {canMap && !closed ? (
              <CopyFromTerm destinationTermId={choice.termId} busy={busy} onPreview={(sourceTermId) => run({ kind: 'copy', sourceTermId }, true)} />
            ) : null}
          </>
        )
      ) : null}
    </div>
  );
}
