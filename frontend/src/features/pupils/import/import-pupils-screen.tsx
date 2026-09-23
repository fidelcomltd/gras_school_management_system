import { useId, useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError, saveFile } from '@/lib/http';
import { formatDate } from '@/shared/format/date';
import { useCommitImport, useDownloadImportTemplate, useValidateImport, type PupilImportReport, type PupilImportResult } from './api';
import { reportToCsv } from './report-csv';

/** Spec 6.5.16: above this the interface warns that the import may take a minute. */
const LARGE_FILE_ROWS = 500;

type Decision = 'skip' | 'create';

function errorMessage(error: unknown): string | null {
  return error instanceof ApiError ? error.message : null;
}

/**
 * `/pupils/import` (spec 6.5.13): download the template, validate a filled-in file (nothing is written), decide each
 * register match, then import all or nothing. Each imported pupil is active with a registration number, issued in file
 * order.
 */
export function ImportPupilsScreen() {
  const fileInputId = useId();
  const me = useMe();
  const canOverride = !!me.data && hasPrivilege(me.data, 'arm.capacity.override');
  const template = useDownloadImportTemplate();
  const validate = useValidateImport();
  const commit = useCommitImport();
  const [file, setFile] = useState<File | null>(null);
  const [decisions, setDecisions] = useState<Record<number, Decision>>({});
  const [overrideCapacity, setOverrideCapacity] = useState(false);

  function reset(next: File | null) {
    setFile(next);
    setDecisions({});
    setOverrideCapacity(false);
    validate.reset();
    commit.reset();
  }

  const report = validate.data;
  if (commit.data) {
    return <ImportDone result={commit.data} onAnother={() => reset(null)} />;
  }

  const matchRows = report?.rows.filter((row) => row.registerMatches.length > 0) ?? [];
  const undecided = matchRows.filter((row) => !decisions[row.sheetRow]).length;
  const skipRows = matchRows.filter((row) => decisions[row.sheetRow] === 'skip').map((row) => row.sheetRow);
  const createRows = matchRows.filter((row) => decisions[row.sheetRow] === 'create').map((row) => row.sheetRow);
  const toImport = report ? report.acceptedCount - skipRows.length : 0;
  // The check counted every accepted row; commit counts only the rows it will create, so a Skip can clear a warning.
  const overCapacity = (report?.capacityWarnings ?? [])
    .map((warning) => ({
      ...warning,
      importCount: warning.importCount - (report?.rows.filter((row) => row.armId === warning.armId && skipRows.includes(row.sheetRow)).length ?? 0),
    }))
    .filter((warning) => warning.currentCount + warning.importCount > warning.capacity);
  const capacityBlocked = overCapacity.length > 0 && !overrideCapacity;
  const canCommit = !!report && !!file && report.rejectedCount === 0 && undecided === 0 && !capacityBlocked && toImport > 0;

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Import pupils</h1>
          <p className="text-sm text-muted-foreground">
            Up to 1,000 pupils from the template. Each is added as active, with a registration number in file order.
          </p>
        </div>
        <Button variant="outline" onClick={() => template.mutate()} disabled={template.isPending}>
          {template.isPending ? 'Downloading…' : 'Download template'}
        </Button>
      </header>
      <FormError message={errorMessage(template.error)} />

      <section className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4">
        <label htmlFor={fileInputId} className="text-sm font-medium text-foreground">
          Filled-in template (.xlsx)
        </label>
        <div className="flex flex-wrap items-center gap-3">
          <input
            id={fileInputId}
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            className="text-sm"
            onChange={(event) => {
              reset(event.target.files?.[0] ?? null);
              // Cleared so choosing the same file again, after fixing it, still fires a change.
              event.target.value = '';
            }}
          />
          {file ? <span className="text-sm text-foreground">{file.name}</span> : null}
          <Button onClick={() => file && validate.mutate(file)} disabled={!file || validate.isPending}>
            {validate.isPending ? 'Checking…' : 'Check file'}
          </Button>
        </div>
        <p className="text-sm text-muted-foreground">Checking writes nothing. You will see every problem before anything is imported.</p>
        <FormError message={errorMessage(validate.error)} />
      </section>

      {report ? (
        <>
          <ReportSummary report={report} />

          {report.rejectedCount > 0 ? <Rejections report={report} /> : null}

          {matchRows.length > 0 ? (
            <section className="flex flex-col gap-3">
              <h2 className="text-lg font-semibold text-foreground">Already on the register?</h2>
              <p className="text-sm text-muted-foreground">
                These rows have the same surname, first name and date of birth as a pupil the school already has. Choose for
                each: skip the row, or create a new pupil anyway (twins and namesakes are real).
              </p>
              <ul className="flex flex-col gap-2">
                {matchRows.map((row) => (
                  <li key={row.sheetRow} className="flex flex-col gap-2 rounded-md border border-border bg-surface px-4 py-3 text-sm">
                    <span className="font-medium text-foreground">
                      Row {row.sheetRow}: {row.surname} {row.firstName}, born {formatDate(row.dateOfBirth)}
                    </span>
                    {row.registerMatches.map((match) => (
                      <Link key={match.pupilId} to={paths.pupilDetail(match.pupilId)} className="text-muted-foreground underline">
                        {match.surname} {match.firstName}: {match.registrationNumber ?? 'no number yet'} ({match.status})
                      </Link>
                    ))}
                    <fieldset className="flex gap-4">
                      <legend className="sr-only">Row {row.sheetRow}</legend>
                      {(['skip', 'create'] as const).map((choice) => (
                        <label key={choice} className="flex items-center gap-2 text-foreground">
                          <input
                            type="radio"
                            name={`decision-${row.sheetRow}`}
                            checked={decisions[row.sheetRow] === choice}
                            onChange={() => setDecisions((current) => ({ ...current, [row.sheetRow]: choice }))}
                          />
                          {choice === 'skip' ? 'Skip this row' : 'Create anyway'}
                        </label>
                      ))}
                    </fieldset>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          {overCapacity.length > 0 ? (
            <section className="flex flex-col gap-2 rounded-md border border-border bg-surface p-4 text-sm">
              <h2 className="text-lg font-semibold text-foreground">Over capacity</h2>
              <ul className="list-disc pl-5 text-foreground">
                {overCapacity.map((warning) => (
                  <li key={warning.armId}>
                    {warning.armName}: {warning.currentCount} enrolled plus {warning.importCount} in this file, against a capacity of{' '}
                    {warning.capacity}.
                  </li>
                ))}
              </ul>
              {canOverride ? (
                <label className="flex items-center gap-2 text-foreground">
                  <input type="checkbox" checked={overrideCapacity} onChange={(event) => setOverrideCapacity(event.target.checked)} />
                  Import past capacity (recorded in the audit log)
                </label>
              ) : (
                <p className="text-muted-foreground">
                  Going past capacity needs the capacity override privilege. Raise the arm&apos;s capacity, move rows to another arm,
                  or ask someone who holds it.
                </p>
              )}
            </section>
          ) : null}

          <div className="flex flex-wrap items-center gap-3">
            <Button
              onClick={() =>
                file &&
                commit.mutate({ file, fileSha256: report.fileSha256, skipRows, createRows, overrideCapacity: overrideCapacity && overCapacity.length > 0 })
              }
              disabled={!canCommit || commit.isPending}
            >
              {commit.isPending ? 'Importing…' : `Import ${toImport} ${toImport === 1 ? 'pupil' : 'pupils'}`}
            </Button>
            {report.rejectedCount > 0 ? (
              <span className="text-sm text-muted-foreground">Fix the rejected rows and check the file again. Nothing imports until every row is valid.</span>
            ) : undecided > 0 ? (
              <span className="text-sm text-muted-foreground">Choose skip or create for {undecided} more {undecided === 1 ? 'row' : 'rows'}.</span>
            ) : null}
          </div>
          <FormError message={errorMessage(commit.error)} />
        </>
      ) : null}
    </div>
  );
}

function ReportSummary({ report }: { report: PupilImportReport }) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-foreground">
          {report.totalRows} rows: <strong>{report.acceptedCount} accepted</strong>, <strong>{report.rejectedCount} rejected</strong>
          {report.registerMatchCount > 0 ? `, ${report.registerMatchCount} already on the register` : ''}.
        </p>
        <Button
          variant="outline"
          size="sm"
          onClick={() => saveFile({ blob: new Blob([reportToCsv(report)], { type: 'text/csv' }), fileName: 'import-report.csv' })}
        >
          Download report (CSV)
        </Button>
      </div>
      {report.totalRows > LARGE_FILE_ROWS ? (
        <output className="block text-sm text-muted-foreground">
          This file has {report.totalRows} pupils. Importing it may take a minute; keep this page open until it finishes.
        </output>
      ) : null}
    </section>
  );
}

function Rejections({ report }: { report: PupilImportReport }) {
  const issues = report.rows.flatMap((row) =>
    row.errors.map((issue, index) => ({ key: `${row.sheetRow}-${index}`, row: row.sheetRow, ...issue })),
  );
  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-lg font-semibold text-foreground">Rejected rows</h2>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="text-muted-foreground">
            <tr>
              <th className="py-2 pr-4 font-medium">Row</th>
              <th className="py-2 pr-4 font-medium">Column</th>
              <th className="py-2 font-medium">Reason</th>
            </tr>
          </thead>
          <tbody>
            {issues.map((issue) => (
              <tr key={issue.key} className="border-t border-border align-top">
                <td className="py-2 pr-4 text-foreground">{issue.row}</td>
                <td className="py-2 pr-4 text-foreground">{issue.column}</td>
                <td className="py-2 text-foreground">{issue.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/**
 * One "first to last" per counter: the serial is the number's trailing digits, and rows from different admission years
 * draw from different counters, so a single first-to-last pair would misstate what was issued.
 */
function numberRanges(result: PupilImportResult): string[] {
  const groups = new Map<string, string[]>();
  for (const pupil of result.pupils) {
    const prefix = pupil.registrationNumber.replace(/\d+$/, '');
    groups.set(prefix, [...(groups.get(prefix) ?? []), pupil.registrationNumber]);
  }
  return [...groups.values()].map((numbers) =>
    numbers.length === 1 ? `${numbers[0]}` : `${numbers[0]} to ${numbers[numbers.length - 1]} (${numbers.length})`,
  );
}

function ImportDone({ result, onAnother }: { result: PupilImportResult; onAnother: () => void }) {
  const ranges = numberRanges(result);
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-foreground">Import complete</h1>
      <output className="block text-sm text-foreground">
        Imported {result.importedCount} {result.importedCount === 1 ? 'pupil' : 'pupils'}
        {ranges.length > 0 ? `, numbered ${ranges.join('; ')}` : ''}
        {result.skippedCount > 0 ? `. Skipped ${result.skippedCount}.` : '.'}
      </output>
      <p className="text-sm text-muted-foreground">
        Their declarations are recorded as unsigned, and health questions left blank stay unanswered rather than No, so the
        office can follow both up with each family.
      </p>
      <div className="flex gap-3">
        <Button render={<Link to={paths.pupils} />}>Go to pupils</Button>
        <Button variant="outline" onClick={onAnother}>
          Import another file
        </Button>
      </div>
    </div>
  );
}
