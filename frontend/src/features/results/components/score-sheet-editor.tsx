import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { cn } from '@/lib/utils/cn';
import { useSaveScoreSheet } from '../api';
import { isEditable, STATE_LABEL, type ScoreSheetDto } from '../types';
import { cellError, draftFrom, hasErrors, rowTotal, toCommand, type RowDraft } from './score-draft';

const inputClass = 'h-9 w-16 rounded-md border border-input bg-background px-2 text-center text-sm';

/**
 * The mark sheet for one arm, subject and term (spec 6.7.4). Blank means not entered, never zero; ABS marks a missed
 * examination. The whole sheet saves at once and a single bad cell blocks the save, as on the server. Keyed by the
 * sheet's version by its parent, so a save (or a reload) starts from the server's values.
 */
export function ScoreSheetEditor({ sheet, canEdit }: { sheet: ScoreSheetDto; canEdit: boolean }) {
  const save = useSaveScoreSheet(sheet.armId, sheet.subjectId, sheet.termId);
  const [initial] = useState(() => draftFrom(sheet));
  const [draft, setDraft] = useState(initial);
  const state = sheet.resultSet?.state;
  const editable = canEdit && isEditable(state);
  const dirty = JSON.stringify(draft) !== JSON.stringify(initial);
  const invalid = hasErrors(sheet, draft);

  const update = (pupilId: string, change: (row: RowDraft) => RowDraft) =>
    setDraft((current) => {
      const row = current[pupilId];
      return row ? { ...current, [pupilId]: change(row) } : current;
    });

  if (sheet.rows.length === 0) {
    return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;
  }

  return (
    <div className="flex flex-col gap-3">
      {state && !isEditable(state) ? (
        <p className="text-sm text-muted-foreground">
          This class's results are {STATE_LABEL[state].toLowerCase()}, so marks are locked.
        </p>
      ) : null}
      {sheet.resultSet?.returnReason ? (
        <p role="note" className="rounded-md bg-warning/10 px-3 py-2 text-sm text-foreground">
          Returned for correction: {sheet.resultSet.returnReason}
        </p>
      ) : null}
      <FormError message={save.error instanceof ApiError ? save.error.message : null} />

      <div className="overflow-x-auto rounded-md border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted text-muted-foreground">
            <tr>
              <th scope="col" className="px-3 py-2 text-left font-medium">Pupil</th>
              {sheet.components.map((c) => (
                <th key={c.id} scope="col" className="px-2 py-2 font-medium">
                  {c.label} <span className="font-normal">/{c.maxMark}</span>
                </th>
              ))}
              <th scope="col" className="px-2 py-2 font-medium">
                {sheet.examination.label} <span className="font-normal">/{sheet.examination.maxMark}</span>
              </th>
              <th scope="col" className="px-2 py-2 font-medium">ABS</th>
              <th scope="col" className="px-2 py-2 font-medium">Total</th>
            </tr>
          </thead>
          <tbody>
            {sheet.rows.map((row) => {
              const entry = draft[row.pupilId];
              if (!entry) return null;
              const examError = entry.absent ? null : cellError(entry.exam, Number(sheet.examination.maxMark));
              return (
                <tr key={row.pupilId} className="border-t border-border">
                  <th scope="row" className="px-3 py-1.5 text-left font-normal text-foreground">
                    {row.displayName}
                    <span className="block text-xs text-muted-foreground">{row.registrationNumber}</span>
                  </th>
                  {sheet.components.map((c) => {
                    const value = entry.marks[c.id] ?? '';
                    const error = cellError(value, Number(c.maxMark));
                    return (
                      <td key={c.id} className="px-2 py-1.5 text-center">
                        <input
                          aria-label={`${c.label} for ${row.displayName}`}
                          aria-invalid={error !== null}
                          title={error ?? undefined}
                          inputMode="numeric"
                          className={cn(inputClass, error && 'border-destructive')}
                          value={value}
                          disabled={!editable}
                          onChange={(event) => update(row.pupilId, (r) => ({ ...r, marks: { ...r.marks, [c.id]: event.target.value } }))}
                        />
                      </td>
                    );
                  })}
                  <td className="px-2 py-1.5 text-center">
                    <input
                      aria-label={`${sheet.examination.label} for ${row.displayName}`}
                      aria-invalid={examError !== null}
                      title={examError ?? undefined}
                      inputMode="numeric"
                      className={cn(inputClass, examError && 'border-destructive')}
                      value={entry.absent ? '' : entry.exam}
                      placeholder={entry.absent ? 'ABS' : undefined}
                      disabled={!editable || entry.absent}
                      onChange={(event) => update(row.pupilId, (r) => ({ ...r, exam: event.target.value }))}
                    />
                  </td>
                  <td className="px-2 py-1.5 text-center">
                    <input
                      type="checkbox"
                      aria-label={`${row.displayName} was absent from the examination`}
                      checked={entry.absent}
                      disabled={!editable}
                      onChange={(event) => update(row.pupilId, (r) => ({ ...r, absent: event.target.checked, exam: '' }))}
                    />
                  </td>
                  <td className="px-2 py-1.5 text-center font-medium text-foreground">{rowTotal(entry) ?? ''}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {editable ? (
        <div className="flex items-center gap-3">
          <Button disabled={!dirty || invalid || save.isPending} onClick={() => save.mutate(toCommand(sheet, draft))}>
            {save.isPending ? 'Saving…' : 'Save marks'}
          </Button>
          <output className="text-sm text-muted-foreground">
            {invalid ? 'Fix the highlighted marks before saving.' : dirty ? 'Unsaved changes.' : 'All marks saved.'}
          </output>
        </div>
      ) : null}
    </div>
  );
}
