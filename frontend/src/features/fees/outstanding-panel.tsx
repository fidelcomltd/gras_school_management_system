import { useState } from 'react';
import { Lock, Users } from 'lucide-react';
import { EmptyState } from '@/components/feedback/empty-state';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useClassChoice } from '@/shared/pickers/use-class-choice';
import { useOutstandingFees, useSaveOutstandingFees } from './api';
import { parseAmount } from './fee-grid-model';
import type { OutstandingFeeSheetDto } from './types';

/** Spec 6.2.13's bulk grid: one arm's pupils against one outstanding figure each. Blank is the normal case. */
export function OutstandingPanel({ sessionId, termId }: { sessionId: string; termId: string }) {
  const klass = useClassChoice(sessionId, 'fee.manage');
  const sheet = useOutstandingFees(klass.armId, termId);
  const save = useSaveOutstandingFees();

  return (
    <div className="flex flex-col gap-4">
      <LabelledSelect
        label="Class"
        placeholder="Class"
        value={klass.armId}
        options={klass.arms.map((arm) => ({ value: arm.id, label: arm.displayName }))}
        onChange={(id) => {
          klass.setArmId(id);
          save.reset();
        }}
        className="w-48"
      />
      {klass.isError ? <QueryErrorState error={new Error('The classes could not be loaded.')} onRetry={klass.retry} /> : null}
      {!klass.isPending && !klass.isError && klass.arms.length === 0 ? (
        <EmptyState icon={Users} title="No classes in this session yet." />
      ) : null}
      {sheet.isPending && klass.armId !== '' ? <LoadingState label="Loading outstanding figures…" /> : null}
      {sheet.isError ? <QueryErrorState error={sheet.error} onRetry={() => void sheet.refetch()} /> : null}
      {sheet.data ? <OutstandingEditor key={`${sheet.data.armId}:${sheet.data.termId}:${sheet.dataUpdatedAt}`} sheet={sheet.data} save={save} /> : null}
    </div>
  );
}

function OutstandingEditor({ sheet, save }: { sheet: OutstandingFeeSheetDto; save: ReturnType<typeof useSaveOutstandingFees> }) {
  const [cells, setCells] = useState<Record<string, string>>(() =>
    Object.fromEntries(sheet.rows.map((row) => [row.pupilId, row.amount == null ? '' : String(row.amount)])),
  );
  const invalid = Object.values(cells).some((cell) => parseAmount(cell) === 'invalid');

  // Only the rows this screen changed are sent: a blank here must never clear a figure someone else saved meanwhile.
  const changed = sheet.rows.flatMap((row) => {
    const parsed = parseAmount(cells[row.pupilId] ?? '');
    return parsed !== 'invalid' && parsed !== (row.amount ?? null) ? [{ pupilId: row.pupilId, amount: parsed }] : [];
  });

  if (sheet.rows.length === 0) {
    return <EmptyState icon={Users} title={`No active pupils in ${sheet.armName}.`} />;
  }

  const submit = () => save.mutate({ armId: sheet.armId, termId: sheet.termId, rows: changed });

  return (
    <section aria-label={`${sheet.armName} outstanding figures`} className="flex flex-col gap-4">
      {sheet.locked ? (
        <p className="flex items-center gap-2 rounded-md bg-muted/60 px-3 py-2 text-sm text-muted-foreground">
          <Lock className="size-4" aria-hidden="true" />
          {sheet.armName}&apos;s {sheet.termName} results are published, so these figures are locked. Withdraw the results to correct one.
        </p>
      ) : (
        <p className="text-sm text-muted-foreground">
          Typed from the school&apos;s own records. Leave blank when nothing is owed; it prints as a dash. No figure ever holds back a result.
        </p>
      )}
      <div className="overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-muted/50 text-xs text-muted-foreground uppercase">
            <tr>
              <th scope="col" className="px-3 py-2 font-semibold">Pupil</th>
              <th scope="col" className="w-48 px-3 py-2 font-semibold">Outstanding (₦)</th>
            </tr>
          </thead>
          <tbody>
            {sheet.rows.map((row) => (
              <tr key={row.pupilId} className="border-t border-border">
                <th scope="row" className="px-3 py-2 font-medium text-foreground">
                  {row.displayName}
                  <span className="block text-xs font-normal text-muted-foreground">{row.registrationNumber}</span>
                </th>
                <td className="px-3 py-2">
                  <input
                    aria-label={`Outstanding for ${row.displayName}`}
                    inputMode="numeric"
                    disabled={sheet.locked}
                    className="h-8 w-full rounded-md border border-input bg-surface px-2 text-right text-sm text-foreground disabled:opacity-60"
                    value={cells[row.pupilId] ?? ''}
                    onChange={(event) => setCells((current) => ({ ...current, [row.pupilId]: event.target.value }))}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {sheet.locked ? null : (
        <div className="flex items-center justify-end gap-3">
          {invalid ? <span className="text-sm text-destructive">Figures are whole naira, 0 or more.</span> : null}
          <Button disabled={invalid || changed.length === 0 || save.isPending} onClick={submit}>
            {save.isPending ? 'Saving…' : 'Save figures'}
          </Button>
        </div>
      )}
      <FormError message={save.error instanceof ApiError ? save.error.message : null} />
      {save.isSuccess ? <output className="text-sm text-success">Saved.</output> : null}
    </section>
  );
}
