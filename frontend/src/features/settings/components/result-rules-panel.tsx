import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useSubjects } from '@/features/subjects/api';
import { ApiError } from '@/lib/http';
import { useUpdateResultRules } from '../api-groups';
import { ReasonField } from './reason-field';

type Rules = components['schemas']['ResultRulesDto'];

const control = 'h-10 rounded-md border border-input bg-background px-2 text-sm';

/**
 * Result rules (spec 6.2.8): annual method and weights, position scope, tie-break, pass mark, promotion threshold and core
 * subjects. Position and tie-break lock once results are published; the annual method once Third Term is. Keyed by version.
 */
export function ResultRulesPanel({ rules, canEdit }: { rules: Rules; canEdit: boolean }) {
  const save = useUpdateResultRules();
  const subjects = useSubjects('Active');
  const [draft, setDraft] = useState(() => ({ ...rules, weightFirst: rules.weightFirst ?? 20, weightSecond: rules.weightSecond ?? 30, weightThird: rules.weightThird ?? 50 }));
  const [reason, setReason] = useState('');
  const set = (change: Partial<typeof draft>) => setDraft((current) => ({ ...current, ...change }));
  const weighted = draft.annualMethod === 'Weighted';
  const weightTotal = Number(draft.weightFirst) + Number(draft.weightSecond) + Number(draft.weightThird);
  const subjectList = subjects.data?.pages.flatMap((page) => page.items) ?? [];
  const number = (label: string, value: number | string, onChange: (value: number) => void) => (
    <label className="flex flex-col gap-1 text-sm">
      {label}
      <input className={`${control} w-24`} inputMode="numeric" disabled={!canEdit} value={value} onChange={(e) => onChange(Number(e.target.value))} />
    </label>
  );

  return (
    <div className="flex max-w-2xl flex-col gap-5">
      <fieldset className="flex flex-wrap items-end gap-4" disabled={!canEdit}>
        <legend className="mb-2 text-sm font-semibold text-foreground">Annual cumulative result</legend>
        <label className="flex flex-col gap-1 text-sm">
          Method
          <select className={control} value={draft.annualMethod} onChange={(e) => set({ annualMethod: e.target.value as Rules['annualMethod'] })}>
            <option value="SimpleAverage">Simple average of the terms</option>
            <option value="Weighted">Weighted by term</option>
          </select>
        </label>
        {weighted ? (
          <>
            {number('First term %', draft.weightFirst, (v) => set({ weightFirst: v }))}
            {number('Second term %', draft.weightSecond, (v) => set({ weightSecond: v }))}
            {number('Third term %', draft.weightThird, (v) => set({ weightThird: v }))}
            <p className={weightTotal === 100 ? 'text-sm text-muted-foreground' : 'text-sm text-warning'}>Weights total {weightTotal}.</p>
          </>
        ) : null}
      </fieldset>

      <fieldset className="flex flex-wrap items-end gap-4" disabled={!canEdit}>
        <legend className="mb-2 text-sm font-semibold text-foreground">Positions</legend>
        <label className="flex flex-col gap-1 text-sm">
          Position within
          <select className={control} value={draft.primaryPositionScope} onChange={(e) => set({ primaryPositionScope: e.target.value as Rules['primaryPositionScope'] })}>
            <option value="Arm">The class (arm)</option>
            <option value="Level">The whole level</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Equal totals
          <select className={control} value={draft.tieBreakRule} onChange={(e) => set({ tieBreakRule: e.target.value as Rules['tieBreakRule'] })}>
            <option value="SharedPosition">Share the position</option>
            <option value="ExamThenCa">Higher exam, then CA</option>
            <option value="ExamThenAlphabetical">Higher exam, then surname</option>
          </select>
        </label>
        {number('Fewest subjects to be ranked', draft.minSubjectsForPosition, (v) => set({ minSubjectsForPosition: v }))}
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={draft.showLevelPosition} onChange={(e) => set({ showLevelPosition: e.target.checked })} />
          Also compute the position in the level
        </label>
      </fieldset>

      <fieldset className="flex flex-col gap-3" disabled={!canEdit}>
        <legend className="mb-2 text-sm font-semibold text-foreground">Pass and promotion</legend>
        <div className="flex flex-wrap gap-4">
          {number('Pass mark', draft.passMark, (v) => set({ passMark: v }))}
          {number('Promotion threshold (annual average)', draft.promotionThreshold, (v) => set({ promotionThreshold: v }))}
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={draft.requireCorePass} onChange={(e) => set({ requireCorePass: e.target.checked })} />
          Promotion also needs a pass in every core subject
        </label>
        {draft.requireCorePass ? (
          <div className="flex flex-wrap gap-3 text-sm">
            {subjectList.map((subject) => (
              <label key={subject.id} className="flex items-center gap-1.5">
                <input
                  type="checkbox"
                  checked={draft.coreSubjectIds.includes(subject.id)}
                  onChange={(e) => set({ coreSubjectIds: e.target.checked ? [...draft.coreSubjectIds, subject.id] : draft.coreSubjectIds.filter((id) => id !== subject.id) })}
                />
                {subject.name}
              </label>
            ))}
          </div>
        ) : null}
      </fieldset>

      {canEdit ? (
        <>
          <ReasonField value={reason} onChange={setReason} />
          <FormError message={save.error instanceof ApiError ? save.error.message : null} />
          <div>
            <Button
              disabled={save.isPending}
              onClick={() =>
                save.mutate({
                  annualMethod: draft.annualMethod,
                  primaryPositionScope: draft.primaryPositionScope,
                  showLevelPosition: draft.showLevelPosition,
                  tieBreakRule: draft.tieBreakRule,
                  passMark: draft.passMark,
                  promotionThreshold: draft.promotionThreshold,
                  requireCorePass: draft.requireCorePass,
                  coreSubjectIds: draft.coreSubjectIds,
                  minSubjectsForPosition: draft.minSubjectsForPosition,
                  weightFirst: weighted ? draft.weightFirst : null,
                  weightSecond: weighted ? draft.weightSecond : null,
                  weightThird: weighted ? draft.weightThird : null,
                  expectedVersion: rules.versionNumber,
                  reason: reason.trim() || null,
                })
              }
            >
              {save.isPending ? 'Saving…' : 'Save result rules'}
            </Button>
          </div>
        </>
      ) : null}
    </div>
  );
}
