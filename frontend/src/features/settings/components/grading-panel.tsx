import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useResetGrading, useUpdateGrading } from '../api-groups';
import { ReasonField } from './reason-field';

type GradingGroup = components['schemas']['SettingsDto']['grading'];
interface Row {
  key: string; // client-only, for React; the server identifies a band by its place in the array
  lowerBound: string;
  upperBound: string;
  gradeLetter: string;
  remark: string;
}

const cell = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

/**
 * The grading scale (spec 6.2.5): whole-number bands from 100 down to 0 with no gaps or overlaps. Saved as one unit;
 * the server checks the ten rules and names the offending band. Keyed by version by its parent.
 */
export function GradingPanel({ grading, canEdit }: { grading: GradingGroup; canEdit: boolean }) {
  const save = useUpdateGrading();
  const reset = useResetGrading();
  const [rows, setRows] = useState<Row[]>(() =>
    grading.bands.map((b) => ({ key: b.id, lowerBound: String(b.lowerBound), upperBound: String(b.upperBound), gradeLetter: b.gradeLetter, remark: b.remark })),
  );
  const [reason, setReason] = useState('');
  const set = (index: number, change: Partial<Row>) => setRows((current) => current.map((row, i) => (i === index ? { ...row, ...change } : row)));
  const error = save.error ?? reset.error;

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <table className="text-sm">
        <thead className="text-muted-foreground">
          <tr>
            <th scope="col" className="py-1 text-left font-medium">From</th>
            <th scope="col" className="py-1 text-left font-medium">To</th>
            <th scope="col" className="py-1 text-left font-medium">Grade</th>
            <th scope="col" className="py-1 text-left font-medium">Remark</th>
            <th scope="col"><span className="sr-only">Remove</span></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={row.key}>
              <td className="py-1 pr-2"><input aria-label={`Band ${index + 1} from`} className={`${cell} w-16`} inputMode="numeric" disabled={!canEdit} value={row.lowerBound} onChange={(e) => set(index, { lowerBound: e.target.value })} /></td>
              <td className="py-1 pr-2"><input aria-label={`Band ${index + 1} to`} className={`${cell} w-16`} inputMode="numeric" disabled={!canEdit} value={row.upperBound} onChange={(e) => set(index, { upperBound: e.target.value })} /></td>
              <td className="py-1 pr-2"><input aria-label={`Band ${index + 1} grade`} className={`${cell} w-16`} disabled={!canEdit} value={row.gradeLetter} onChange={(e) => set(index, { gradeLetter: e.target.value })} /></td>
              <td className="py-1 pr-2"><input aria-label={`Band ${index + 1} remark`} className={`${cell} w-48`} disabled={!canEdit} value={row.remark} onChange={(e) => set(index, { remark: e.target.value })} /></td>
              <td className="py-1">
                {canEdit ? (
                  <Button size="sm" variant="ghost" onClick={() => setRows((current) => current.filter((_, i) => i !== index))}>
                    Remove <span className="sr-only">band {index + 1}</span>
                  </Button>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {canEdit ? (
        <>
          <div>
            <Button variant="outline" size="sm" onClick={() => setRows((current) => [...current, { key: crypto.randomUUID(), lowerBound: '', upperBound: '', gradeLetter: '', remark: '' }])}>
              Add band
            </Button>
          </div>
          <ReasonField value={reason} onChange={setReason} />
          <FormError message={error instanceof ApiError ? error.message : null} />
          <div className="flex gap-2">
            <Button
              disabled={save.isPending}
              onClick={() =>
                save.mutate({
                  bands: rows.map((row) => ({ lowerBound: Number(row.lowerBound), upperBound: Number(row.upperBound), gradeLetter: row.gradeLetter.trim(), remark: row.remark.trim() })),
                  expectedVersion: grading.versionNumber,
                  reason: reason.trim() || null,
                })
              }
            >
              {save.isPending ? 'Saving…' : 'Save grading scale'}
            </Button>
            <Button variant="ghost" disabled={reset.isPending} onClick={() => reset.mutate({ expectedVersion: grading.versionNumber, reason: reason.trim() || null })}>
              Reset to the school's standard scale
            </Button>
          </div>
        </>
      ) : null}
    </div>
  );
}
