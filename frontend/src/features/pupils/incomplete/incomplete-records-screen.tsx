import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { saveFile } from '@/lib/http';
import { csvDocument, csvLine } from '@/shared/format/csv';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useIncompleteRecords, type IncompleteRecord, type IncompleteRecordsReport } from './api';

const ALL_ARMS = 'all';

function fullName(pupil: IncompleteRecord): string {
  return [pupil.surname.toUpperCase(), pupil.firstName, pupil.middleName].filter(Boolean).join(' ');
}

function gaps(pupil: IncompleteRecord) {
  return [...pupil.required, ...pupil.chased];
}

function toCsv(pupils: IncompleteRecord[], labels: Map<string, string>): string {
  const lines = [csvLine(['Registration number', 'Name', 'Arm', 'Chased complete %', 'Required missing', 'Chased missing'])];
  for (const pupil of pupils) {
    const names = (items: IncompleteRecord['required']) => items.map((item) => labels.get(item.code) ?? item.message).join('; ');
    lines.push(csvLine([pupil.registrationNumber, fullName(pupil), pupil.armName, pupil.chasedPercent, names(pupil.required), names(pupil.chased)]));
  }
  return csvDocument(lines);
}

/**
 * `/reports/incomplete-records` (spec 6.5.12): the office's chasing list. Active pupils with something still missing,
 * filtered by arm and by gap; the counts answer "how many have no emergency contact" at a glance.
 */
export function IncompleteRecordsScreen() {
  const report = useIncompleteRecords();

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Incomplete records</h1>
        <p className="text-sm text-muted-foreground">
          Active pupils with something still to collect. Required items are ones approval normally insists on, missing after a
          bulk import or a health override.
        </p>
      </header>
      {report.isPending ? (
        <LoadingState label="Loading incomplete records…" />
      ) : report.isError ? (
        <QueryErrorState error={report.error} onRetry={() => void report.refetch()} />
      ) : (
        <Report report={report.data} />
      )}
    </div>
  );
}

function Report({ report }: { report: IncompleteRecordsReport }) {
  const me = useMe();
  const canExport = !!me.data && hasPrivilege(me.data, 'report.export');
  const [armId, setArmId] = useState(ALL_ARMS);
  const [code, setCode] = useState<string | null>(null);

  if (report.sessionName === null) {
    return <p className="text-sm text-muted-foreground">No session is active, so there is no roll to check.</p>;
  }

  const arms = [...new Map(report.pupils.map((pupil) => [pupil.armId, pupil.armName])).entries()];
  const inArm = armId === ALL_ARMS ? report.pupils : report.pupils.filter((pupil) => pupil.armId === armId);
  const shown = code === null ? inArm : inArm.filter((pupil) => gaps(pupil).some((item) => item.code === code));
  const labels = new Map(report.counts.map((count) => [count.code, count.label]));
  // Counts follow the arm filter; the server's counts cover the whole report and supply the labels and their order.
  const counts = report.counts
    .map((count) => ({ ...count, count: inArm.filter((pupil) => gaps(pupil).some((item) => item.code === count.code)).length }))
    .filter((count) => count.count > 0);

  return (
    <>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <p className="text-sm text-foreground">
          {report.sessionName}: {report.pupils.length} of {report.pupilsChecked} active pupils have something missing.
        </p>
        <div className="flex flex-wrap items-center gap-3">
          <LabelledSelect
            label="Arm"
            placeholder="All arms"
            value={armId}
            options={[{ value: ALL_ARMS, label: 'All arms' }, ...arms.map(([id, name]) => ({ value: id, label: name }))]}
            onChange={(next) => setArmId(next)}
          />
          {canExport ? (
            <Button
              variant="outline"
              size="sm"
              disabled={shown.length === 0}
              onClick={() => saveFile({ blob: new Blob([toCsv(shown, labels)], { type: 'text/csv' }), fileName: 'incomplete-records.csv' })}
            >
              Download CSV
            </Button>
          ) : null}
        </div>
      </div>

      {counts.length > 0 ? (
        <fieldset className="flex flex-wrap gap-2">
          <legend className="sr-only">Filter by what is missing</legend>
          {counts.map((count) => (
            <Button
              key={count.code}
              size="sm"
              variant={code === count.code ? 'primary' : 'outline'}
              aria-pressed={code === count.code}
              onClick={() => setCode(code === count.code ? null : count.code)}
            >
              {count.label}: {count.count}
            </Button>
          ))}
        </fieldset>
      ) : null}

      {shown.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing to chase here.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-muted-foreground">
              <tr>
                <th className="py-2 pr-4 font-medium">Pupil</th>
                <th className="py-2 pr-4 font-medium">Arm</th>
                <th className="py-2 pr-4 font-medium">Required, missing</th>
                <th className="py-2 pr-4 font-medium">Still to collect</th>
                <th className="py-2 font-medium">Complete</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((pupil) => (
                <tr key={pupil.pupilId} className="border-t border-border align-top">
                  <td className="py-2 pr-4">
                    <Link to={paths.pupilDetail(pupil.pupilId)} className="font-medium text-foreground underline">
                      {fullName(pupil)}
                    </Link>
                    <div className="text-muted-foreground">{pupil.registrationNumber}</div>
                  </td>
                  <td className="py-2 pr-4 text-foreground">{pupil.armName}</td>
                  <td className="py-2 pr-4 text-destructive">{pupil.required.map((item) => labels.get(item.code) ?? item.message).join(', ')}</td>
                  <td className="py-2 pr-4 text-foreground">{pupil.chased.map((item) => labels.get(item.code) ?? item.message).join(', ')}</td>
                  <td className="py-2 text-foreground">{pupil.chasedPercent}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
