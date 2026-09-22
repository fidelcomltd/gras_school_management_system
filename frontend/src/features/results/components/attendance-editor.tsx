import { useState } from 'react';
import type { components } from '@/api/schema';
import { cn } from '@/lib/utils/cn';
import { useSaveAttendance } from '../api-records';
import { isEditable } from '../types';
import { LockNotice, SaveBar } from './save-bar';

type AttendanceSheetDto = components['schemas']['AttendanceSheetDto'];

/**
 * Times present per pupil (spec 6.7.7, Appendix F.1). Times absent is derived from times school opened, never typed.
 * Keyed by version by its parent, so a save starts again from the server's values.
 */
export function AttendanceEditor({ sheet, canEdit }: { sheet: AttendanceSheetDto; canEdit: boolean }) {
  const save = useSaveAttendance(sheet.armId, sheet.termId);
  const [initial] = useState(() => Object.fromEntries(sheet.rows.map((row) => [row.pupilId, row.timesPresent?.toString() ?? ''])));
  const [draft, setDraft] = useState(initial);
  const opened = sheet.timesSchoolOpened == null ? null : Number(sheet.timesSchoolOpened);
  const editable = canEdit && isEditable(sheet.resultSet?.state);
  const invalid = (value: string) =>
    value.trim() !== '' && (!/^\d+$/.test(value.trim()) || (opened !== null && Number(value) > opened));
  const anyInvalid = Object.values(draft).some(invalid);

  if (sheet.rows.length === 0) return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;

  return (
    <div className="flex flex-col gap-3">
      {!isEditable(sheet.resultSet?.state) ? <LockNotice state={sheet.resultSet?.state} what="attendance" /> : null}
      <p className="text-sm text-muted-foreground">
        {opened === null ? 'Times school opened is not set for this term yet (Sessions → term).' : `School opened ${opened} times this term.`}
      </p>
      <table className="w-full max-w-xl text-sm">
        <thead className="text-muted-foreground">
          <tr>
            <th scope="col" className="py-2 text-left font-medium">Pupil</th>
            <th scope="col" className="py-2 font-medium">Times present</th>
            <th scope="col" className="py-2 font-medium">Times absent</th>
          </tr>
        </thead>
        <tbody>
          {sheet.rows.map((row) => {
            const value = draft[row.pupilId] ?? '';
            const bad = invalid(value);
            return (
              <tr key={row.pupilId} className="border-t border-border">
                <th scope="row" className="py-1.5 text-left font-normal text-foreground">{row.displayName}</th>
                <td className="py-1.5 text-center">
                  <input
                    aria-label={`Times present for ${row.displayName}`}
                    aria-invalid={bad}
                    inputMode="numeric"
                    className={cn('h-9 w-20 rounded-md border border-input bg-background px-2 text-center', bad && 'border-destructive')}
                    value={value}
                    disabled={!editable}
                    onChange={(event) => setDraft((current) => ({ ...current, [row.pupilId]: event.target.value }))}
                  />
                </td>
                <td className="py-1.5 text-center text-muted-foreground">
                  {opened !== null && value.trim() !== '' && !bad ? opened - Number(value) : ''}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {editable ? (
        <SaveBar
          label="Save attendance"
          dirty={JSON.stringify(draft) !== JSON.stringify(initial)}
          invalidMessage={anyInvalid ? `Times present must be a whole number${opened === null ? '' : ` from 0 to ${opened}`}.` : null}
          pending={save.isPending}
          error={save.error}
          onSave={() =>
            save.mutate({
              armId: sheet.armId,
              termId: sheet.termId,
              version: sheet.version ?? null,
              rows: sheet.rows.map((row) => {
                const value = (draft[row.pupilId] ?? '').trim();
                return { pupilId: row.pupilId, timesPresent: value === '' ? null : Number(value) };
              }),
            })
          }
        />
      ) : null}
    </div>
  );
}
