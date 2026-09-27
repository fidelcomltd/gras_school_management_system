import { FileDown, HeartPulse, ShieldAlert, UserRound } from 'lucide-react';
import { useState } from 'react';
import { EmptyState } from '@/components/feedback/empty-state';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { usePupilPhotoUrl } from '../records/files-api';
import { errorText } from '../records/format';
import { useClassChoice } from '@/shared/pickers/use-class-choice';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { downloadSafeguardingSheet, useSafeguardingSheet, type SafeguardingSheetRowDto } from './api';

/**
 * `/reports/safeguarding` (spec 15 section 10.2): the class safeguarding sheet, printed before an excursion and kept at the
 * gate. The one screen that shows a class's health data together; every generation (on screen or PDF) is audited, so the
 * sheet is only generated when asked for.
 */
export function SafeguardingSheetScreen() {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'pupil.safeguarding.view');
  const [requested, setRequested] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<unknown>(null);
  const sheet = useSafeguardingSheet(klass.armId, requested === klass.armId);

  const download = () => {
    setDownloading(true);
    setDownloadError(null);
    downloadSafeguardingSheet(klass.armId)
      .catch(setDownloadError)
      .finally(() => setDownloading(false));
  };

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
            <Button onClick={() => setRequested(klass.armId)} disabled={!klass.armId || (requested === klass.armId && sheet.isFetching)}>
              <HeartPulse aria-hidden="true" />
              Show sheet
            </Button>
            <Button variant="outline" onClick={download} disabled={!klass.armId || downloading}>
              {downloading ? <Spinner className="size-4" /> : <FileDown aria-hidden="true" />}
              {downloading ? 'Preparing PDF…' : 'Download PDF'}
            </Button>
          </div>
          <FormError message={errorText(downloadError)} />
          {requested !== klass.armId ? null : sheet.isPending ? (
            <LoadingState label="Preparing the sheet…" />
          ) : sheet.isError ? (
            <QueryErrorState error={sheet.error} onRetry={() => void sheet.refetch()} />
          ) : sheet.data.pupils.length === 0 ? (
            <EmptyState icon={UserRound} title="No active pupils in this class." />
          ) : (
            <SheetTable rows={sheet.data.pupils} />
          )}
        </>
      )}
    </div>
  );
}

const HEADINGS = ['Pupil', 'Allergies', 'Medical conditions', 'Medication', 'Special instructions', 'Hospital', 'Authorised pickup', 'Barred'];

function SheetTable({ rows }: { rows: SafeguardingSheetRowDto[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full min-w-[960px] text-left text-sm">
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
                  <Thumbnail row={row} />
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

function Thumbnail({ row }: { row: SafeguardingSheetRowDto }) {
  const photo = usePupilPhotoUrl(row.pupilId, row.photoUpdatedAtUtc, true);
  return (
    <span className="grid size-9 shrink-0 place-items-center overflow-hidden rounded-md bg-muted">
      {photo.data ? <img src={photo.data} alt="" className="size-full object-cover" /> : <UserRound className="size-4 text-muted-foreground" aria-hidden="true" />}
    </span>
  );
}
