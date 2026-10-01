import { FileDown, HeartPulse, ShieldAlert, UserRound } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/feedback/empty-state';
import { FormError, LoadingState } from '@/components/feedback/query-states';
import { Spinner } from '@/components/ui/spinner';
import { lagosDateTime } from '@/shared/format/date';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useClassChoice } from '@/shared/pickers/use-class-choice';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { errorText } from '../records/format';
import { useDownloadSafeguardingSheet, useGenerateSafeguardingSheet, type SafeguardingSheetRowDto } from './api';

/**
 * `/reports/safeguarding` (spec 15 section 10.2): the class safeguarding sheet, printed before an excursion and kept at the
 * gate. The one screen that shows a class's health data together. Each Show sheet or Download PDF is a fresh generation,
 * audited on the server, and the sheet says when it was generated, so a stale copy is recognisable.
 */
export function SafeguardingSheetScreen() {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'pupil.safeguarding.view');
  const generate = useGenerateSafeguardingSheet();
  const download = useDownloadSafeguardingSheet();
  // Only the sheet for the class now chosen is shown: switching class hides the previous one.
  const shown = generate.data?.armId === klass.armId ? generate.data : undefined;

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Safeguarding sheet</h1>
        <p className="text-sm text-muted-foreground">
          Health and collection details for one class, for an excursion or the gate. Confidential: each copy you show or print
          is recorded.
        </p>
      </header>

      {term.isPending || klass.isPending ? (
        <LoadingState label="Loading classes…" />
      ) : klass.isError ? (
        <div role="alert" className="flex flex-col items-start gap-3">
          <p className="text-sm text-destructive">The classes could not be loaded. Check the connection and try again.</p>
          <Button variant="outline" size="sm" onClick={klass.retry}>
            Try again
          </Button>
        </div>
      ) : klass.arms.length === 0 ? (
        <EmptyState icon={ShieldAlert} title="There are no classes whose safeguarding details you can view." />
      ) : (
        <>
          <div className="flex flex-wrap items-end gap-3">
            <LabelledSelect
              label="Class"
              placeholder="Class"
              value={klass.armId}
              options={klass.arms.map((arm) => ({ value: arm.id, label: arm.displayName }))}
              onChange={klass.setArmId}
              className="w-48"
            />
            <Button onClick={() => generate.mutate(klass.armId)} disabled={!klass.armId || generate.isPending}>
              {generate.isPending ? <Spinner className="size-4 text-primary-foreground" /> : <HeartPulse aria-hidden="true" />}
              {shown ? 'Show again' : 'Show sheet'}
            </Button>
            <Button variant="outline" onClick={() => download.mutate(klass.armId)} disabled={!klass.armId || download.isPending}>
              {download.isPending ? <Spinner className="size-4" /> : <FileDown aria-hidden="true" />}
              {download.isPending ? 'Preparing PDF…' : 'Download PDF'}
            </Button>
          </div>
          <FormError message={errorText(download.error) ?? errorText(generate.error)} />
          {generate.isPending ? (
            <LoadingState label="Preparing the sheet…" />
          ) : shown ? (
            shown.pupils.length === 0 ? (
              <EmptyState icon={UserRound} title="No active pupils in this class." />
            ) : (
              <>
                <p className="text-xs text-muted-foreground">
                  {shown.armName}, {shown.sessionName}. Generated {lagosDateTime(shown.generatedAtUtc)}.
                </p>
                <SheetTable rows={shown.pupils} />
              </>
            )
          ) : null}
        </>
      )}
    </div>
  );
}

const HEADINGS = ['Pupil', 'Allergies', 'Medical conditions', 'Medication', 'Special instructions', 'Hospital', 'Authorised pickup', 'Barred'];

function SheetTable({ rows }: { rows: SafeguardingSheetRowDto[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full min-w-240 text-left text-sm">
        <thead className="bg-muted/50 text-xs text-muted-foreground uppercase">
          <tr>
            {HEADINGS.map((heading) => (
              <th key={heading} scope="col" className="px-3 py-2 font-semibold">
                {heading}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.pupilId} className="border-t border-border align-top">
              <th scope="row" className="px-3 py-2 font-medium text-foreground">
                <span className="flex items-center gap-2">
                  <span className="grid size-9 shrink-0 place-items-center overflow-hidden rounded-md bg-muted">
                    {row.thumbnail ? (
                      <img src={row.thumbnail} alt="" className="size-full object-cover" />
                    ) : (
                      <UserRound className="size-4 text-muted-foreground" aria-hidden="true" />
                    )}
                  </span>
                  {row.name}
                </span>
              </th>
              <td className="px-3 py-2">{row.allergies}</td>
              <td className="px-3 py-2">{row.medicalConditions}</td>
              <td className="px-3 py-2">{row.medication}</td>
              <td className="px-3 py-2">{row.specialInstructions}</td>
              <td className="px-3 py-2">{row.hospital}</td>
              <td className="px-3 py-2">
                {row.pickupPersons.map((person) => (
                  <span key={person} className="block">
                    {person}
                  </span>
                ))}
              </td>
              <td className={row.barredMarker.startsWith('Yes') ? 'px-3 py-2 font-semibold text-destructive' : 'px-3 py-2'}>{row.barredMarker}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
