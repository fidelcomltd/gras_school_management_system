import { useState } from 'react';
import type { components } from '@/api/schema';
import { Input } from '@/components/ui/input';
import { useRemarkTemplates, useSaveClassRemarks, useSaveHeadRemarks } from '../api-records';
import type { ResultSetState } from '../types';
import { RemarkPhrases } from './remark-phrases';
import { LockNotice, SaveBar } from './save-bar';

type RemarkSheetDto = components['schemas']['RemarkSheetDto'];
const MAX = 300;

/** Spec 6.7.7: the class teacher writes while Draft or returned; the head teacher also while awaiting approval or approved. */
function isOpen(kind: 'ClassTeacher' | 'HeadTeacher', state: ResultSetState | undefined): boolean {
  if (state === undefined || state === 'Draft' || state === 'ReturnedForCorrection') return true;
  return kind === 'HeadTeacher' && (state === 'AwaitingApproval' || state === 'Approved');
}

/**
 * One remark per pupil (spec 6.7.7, Appendix C.6). Saves only the rows that changed; an emptied remark clears it. The
 * head teacher can also fill every still-empty remark with one phrase (ruling H). Keyed by version by its parent.
 */
export function RemarksEditor({ kind, sheet, canEdit }: { kind: 'ClassTeacher' | 'HeadTeacher'; sheet: RemarkSheetDto; canEdit: boolean }) {
  const saveClass = useSaveClassRemarks(sheet.armId, sheet.termId);
  const saveHead = useSaveHeadRemarks(sheet.armId, sheet.termId);
  const save = kind === 'ClassTeacher' ? saveClass : saveHead;
  const templates = useRemarkTemplates(kind);
  const [initial] = useState(() => Object.fromEntries(sheet.rows.map((row) => [row.pupilId, row.remark ?? ''])));
  const [draft, setDraft] = useState(initial);
  const [fillEmpty, setFillEmpty] = useState('');
  const state = sheet.resultSet?.state;
  const editable = canEdit && isOpen(kind, state);
  const phrases = templates.data?.templates ?? [];
  const tooLong = [...Object.values(draft), fillEmpty].some((text) => text.trim().length > MAX);
  const changed = sheet.rows.filter((row) => (draft[row.pupilId] ?? '') !== (initial[row.pupilId] ?? ''));

  const onSave = () => {
    const rows = changed.map((row) => ({ pupilId: row.pupilId, remark: (draft[row.pupilId] ?? '').trim() || null }));
    const base = { armId: sheet.armId, termId: sheet.termId, version: sheet.version ?? null, rows };
    if (kind === 'ClassTeacher') saveClass.mutate(base);
    else saveHead.mutate({ ...base, fillEmpty: fillEmpty.trim() || null });
  };

  if (sheet.rows.length === 0) return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;

  return (
    <div className="flex flex-col gap-4">
      {!isOpen(kind, state) ? <LockNotice state={state} what="these remarks" /> : null}
      <ul className="flex flex-col gap-3">
        {sheet.rows.map((row) => {
          const value = draft[row.pupilId] ?? '';
          return (
            <li key={row.pupilId} className="flex flex-col gap-1.5 rounded-md border border-border bg-surface p-3">
              <div className="flex flex-wrap items-baseline justify-between gap-2 text-sm">
                <span className="font-medium text-foreground">{row.displayName}</span>
                {row.writtenByName ? <span className="text-xs text-muted-foreground">Last written by {row.writtenByName}</span> : null}
              </div>
              <textarea
                aria-label={`Remark for ${row.displayName}`}
                className="min-h-16 rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={value}
                disabled={!editable}
                onChange={(event) => setDraft((current) => ({ ...current, [row.pupilId]: event.target.value }))}
              />
              <div className="flex flex-wrap items-center justify-between gap-2">
                {editable && phrases.length > 0 ? (
                  <select
                    aria-label={`Insert a saved phrase for ${row.displayName}`}
                    className="h-8 max-w-xs rounded-md border border-input bg-background px-2 text-sm"
                    value=""
                    onChange={(event) => event.target.value && setDraft((current) => ({ ...current, [row.pupilId]: event.target.value }))}
                  >
                    <option value="">Use a saved phrase…</option>
                    {phrases.map((phrase) => (
                      <option key={phrase.id} value={phrase.text}>
                        {phrase.text}
                      </option>
                    ))}
                  </select>
                ) : (
                  <span />
                )}
                <span className={value.trim().length > MAX ? 'text-xs text-destructive' : 'text-xs text-muted-foreground'}>
                  {value.trim().length}/{MAX}
                </span>
              </div>
            </li>
          );
        })}
      </ul>

      {editable && kind === 'HeadTeacher' ? (
        <div className="flex flex-col gap-1 text-sm text-foreground">
          <label htmlFor="fill-empty">Fill every pupil still without a remark with (optional)</label>
          <Input id="fill-empty" value={fillEmpty} onChange={(event) => setFillEmpty(event.target.value)} />
        </div>
      ) : null}
      {editable ? (
        <SaveBar
          label="Save remarks"
          dirty={changed.length > 0 || fillEmpty.trim() !== ''}
          invalidMessage={tooLong ? `A remark can be at most ${MAX} characters.` : null}
          pending={save.isPending}
          error={save.error}
          onSave={onSave}
        />
      ) : null}
      {editable ? <RemarkPhrases kind={kind} phrases={phrases} /> : null}
    </div>
  );
}
