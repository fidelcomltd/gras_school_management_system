import { TriangleAlert, Users } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import type { components } from '@/api/schema';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { useSaveAttendance } from '../api-records';
import { isEditable } from '../types';
import { LockNotice, SaveBar } from './save-bar';
import { EmptyState } from '@/components/feedback/empty-state';

type AttendanceSheetDto = components['schemas']['AttendanceSheetDto'];

/**
 * Times present per pupil (spec 6.7.7, Appendix F.1). Times absent is derived from times school opened, never typed.
 * Keyed by version by its parent, so a save starts again from the server's values.
 */
export function AttendanceEditor({ sheet, canEdit }: { sheet: AttendanceSheetDto; canEdit: boolean }) {
  const save = useSaveAttendance(sheet.armId, sheet.termId);
  const me = useMe();
  const canSetOpened = !!me.data && hasPrivilege(me.data, 'session.update');
  const [initial] = useState(() => Object.fromEntries(sheet.rows.map((row) => [row.pupilId, row.timesPresent?.toString() ?? ''])));
  const [draft, setDraft] = useState(initial);
  const opened = sheet.timesSchoolOpened == null ? null : Number(sheet.timesSchoolOpened);
  const editable = canEdit && isEditable(sheet.resultSet?.state);
  const invalid = (value: string) =>
    value.trim() !== '' && (!/^\d+$/.test(value.trim()) || (opened !== null && Number(value) > opened));
  const anyInvalid = Object.values(draft).some(invalid);

  if (sheet.rows.length === 0) return <EmptyState icon={Users} title="No active pupils in this class." />;

  return (
    <div className="flex flex-col gap-3">
      {!isEditable(sheet.resultSet?.state) ? <LockNotice state={sheet.resultSet?.state} what="attendance" /> : null}
      {opened === null ? (
        // Times absent is worked out, never typed, so without this number the absent column cannot fill in: say so plainly.
        <div className="flex max-w-xl gap-2 rounded-md border border-warning/40 bg-warning/10 p-3 text-sm text-foreground">
          <TriangleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden="true" />
          <p>
            <strong>Times absent will fill in once the term's "Times school opened" is set</strong> (the number of days school opened this
            term, e.g. 62). Times absent is worked out as times school opened minus times present, so it is never typed here. Times
            present can be entered and saved now.{' '}
            {canSetOpened ? (
              <>
                Set it under{' '}
                <Link to="/sessions" className="font-medium text-primary underline">
                  Sessions
                </Link>{' '}
                → this session → this term → Edit.
              </>
            ) : (
              'Ask whoever manages sessions to set it on this term.'
            )}
          </p>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          School opened {opened} times this term. Times absent is worked out as {opened} minus times present.
        </p>
      )}
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
