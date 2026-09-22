import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useUpdateAssessment } from '../api-groups';
import { ReasonField } from './reason-field';

type AssessmentGroup = components['schemas']['SettingsDto']['assessment'];
interface Row {
  id: string | null;
  name: string;
  shortLabel: string;
  maxMark: string;
  isExamination: boolean;
}

const cell = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

/**
 * The assessment structure (spec 6.2.6): the continuous assessments and the examination, with their maximum marks. Once
 * a mark is entered in the session the structure locks; renaming and reordering stay allowed. Keyed by version.
 */
export function AssessmentPanel({ assessment, canEdit }: { assessment: AssessmentGroup; canEdit: boolean }) {
  const save = useUpdateAssessment();
  const [rows, setRows] = useState<Row[]>(() =>
    assessment.components.map((c) => ({ id: c.id, name: c.name, shortLabel: c.shortLabel, maxMark: String(c.maxMark), isExamination: c.isExamination })),
  );
  const [reason, setReason] = useState('');
  const set = (index: number, change: Partial<Row>) => setRows((current) => current.map((row, i) => (i === index ? { ...row, ...change } : row)));
  const total = rows.reduce((sum, row) => sum + (Number(row.maxMark) || 0), 0);
  const move = (index: number, by: -1 | 1) =>
    setRows((current) => {
      const next = [...current];
      const [row] = next.splice(index, 1);
      if (row) next.splice(index + by, 0, row);
      return next;
    });

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <table className="text-sm">
        <thead className="text-muted-foreground">
          <tr>
            <th scope="col" className="py-1 text-left font-medium">Name</th>
            <th scope="col" className="py-1 text-left font-medium">Column label</th>
            <th scope="col" className="py-1 text-left font-medium">Maximum</th>
            <th scope="col" className="py-1 text-left font-medium">Examination</th>
            <th scope="col"><span className="sr-only">Order and remove</span></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={row.id ?? `new-${index}`}>
              <td className="py-1 pr-2"><input aria-label={`Component ${index + 1} name`} className={`${cell} w-40`} disabled={!canEdit} value={row.name} onChange={(e) => set(index, { name: e.target.value })} /></td>
              <td className="py-1 pr-2"><input aria-label={`Component ${index + 1} label`} className={`${cell} w-24`} disabled={!canEdit} value={row.shortLabel} onChange={(e) => set(index, { shortLabel: e.target.value })} /></td>
              <td className="py-1 pr-2"><input aria-label={`Component ${index + 1} maximum`} className={`${cell} w-16`} inputMode="numeric" disabled={!canEdit} value={row.maxMark} onChange={(e) => set(index, { maxMark: e.target.value })} /></td>
              <td className="py-1 pr-2 text-center">
                <input type="radio" name="examination" aria-label={`Component ${index + 1} is the examination`} disabled={!canEdit} checked={row.isExamination} onChange={() => setRows((current) => current.map((r, i) => ({ ...r, isExamination: i === index })))} />
              </td>
              <td className="flex gap-1 py-1">
                {canEdit ? (
                  <>
                    <Button size="sm" variant="ghost" disabled={index === 0} onClick={() => move(index, -1)} aria-label={`Move component ${index + 1} up`}>↑</Button>
                    <Button size="sm" variant="ghost" disabled={index === rows.length - 1} onClick={() => move(index, 1)} aria-label={`Move component ${index + 1} down`}>↓</Button>
                    <Button size="sm" variant="ghost" onClick={() => setRows((current) => current.filter((_, i) => i !== index))}>
                      Remove <span className="sr-only">component {index + 1}</span>
                    </Button>
                  </>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className={total === 100 ? 'text-sm text-muted-foreground' : 'text-sm text-warning'}>Maximum marks add up to {total}.</p>

      {canEdit ? (
        <>
          <div>
            <Button variant="outline" size="sm" onClick={() => setRows((current) => [...current, { id: null, name: '', shortLabel: '', maxMark: '', isExamination: false }])}>
              Add assessment
            </Button>
          </div>
          <ReasonField value={reason} onChange={setReason} />
          <FormError message={save.error instanceof ApiError ? save.error.message : null} />
          <div>
            <Button
              disabled={save.isPending}
              onClick={() =>
                save.mutate({
                  components: rows.map((row) => ({ id: row.id, name: row.name.trim(), shortLabel: row.shortLabel.trim(), maxMark: Number(row.maxMark), isExamination: row.isExamination })),
                  expectedVersion: assessment.versionNumber,
                  reason: reason.trim() || null,
                })
              }
            >
              {save.isPending ? 'Saving…' : 'Save assessment structure'}
            </Button>
          </div>
        </>
      ) : null}
    </div>
  );
}
