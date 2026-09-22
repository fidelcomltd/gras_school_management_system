import { useState } from 'react';
import type { components } from '@/api/schema';
import { useSaveTraitRatings } from '../api-records';
import { isEditable } from '../types';
import { LockNotice, SaveBar } from './save-bar';

type TraitRatingSheetDto = components['schemas']['TraitRatingSheetDto'];
type Draft = Record<string, Record<string, string>>;

const selectClass = 'h-8 rounded-md border border-input bg-background px-1 text-sm';

/** Affective and psychomotor ratings for a primary class (spec 6.7.7, Appendix F.3). Keyed by version by its parent. */
export function TraitRatingsEditor({ sheet, canEdit }: { sheet: TraitRatingSheetDto; canEdit: boolean }) {
  const save = useSaveTraitRatings(sheet.armId, sheet.termId);
  const [initial] = useState<Draft>(() =>
    Object.fromEntries(sheet.rows.map((row) => [row.pupilId, Object.fromEntries(Object.entries(row.ratings).map(([trait, point]) => [trait, point ?? '']))])),
  );
  const [draft, setDraft] = useState(initial);
  const state = sheet.resultSet?.state;
  const editable = canEdit && isEditable(state);

  if (sheet.rows.length === 0) return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;

  return (
    <div className="flex flex-col gap-5">
      {!isEditable(state) ? <LockNotice state={state} what="ratings" /> : null}
      {sheet.blocks.map((block) => (
        <section key={block.domain} aria-label={`${block.domain} ratings`} className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold text-foreground">{block.domain === 'Affective' ? 'Affective domain' : 'Psychomotor skills'}</h2>
          <p className="text-xs text-muted-foreground">
            {block.scale.points.map((point) => `${point.pointCode} = ${point.pointLabel}`).join(' · ')}
          </p>
          <div className="overflow-x-auto rounded-md border border-border">
            <table className="w-full text-sm">
              <thead className="bg-muted text-muted-foreground">
                <tr>
                  <th scope="col" className="px-3 py-2 text-left font-medium">Pupil</th>
                  {block.traits.map((trait) => (
                    <th key={trait.id} scope="col" className="px-2 py-2 font-medium">{trait.name}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {sheet.rows.map((row) => (
                  <tr key={row.pupilId} className="border-t border-border">
                    <th scope="row" className="px-3 py-1.5 text-left font-normal text-foreground">{row.displayName}</th>
                    {block.traits.map((trait) => (
                      <td key={trait.id} className="px-2 py-1.5 text-center">
                        <select
                          aria-label={`${trait.name} for ${row.displayName}`}
                          className={selectClass}
                          disabled={!editable}
                          value={draft[row.pupilId]?.[trait.id] ?? ''}
                          onChange={(event) =>
                            setDraft((current) => ({ ...current, [row.pupilId]: { ...current[row.pupilId], [trait.id]: event.target.value } }))
                          }
                        >
                          <option value="">–</option>
                          {block.scale.points.map((point) => (
                            <option key={point.id} value={point.id}>{point.pointCode}</option>
                          ))}
                        </select>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
      {editable ? (
        <SaveBar
          label="Save ratings"
          dirty={JSON.stringify(draft) !== JSON.stringify(initial)}
          pending={save.isPending}
          error={save.error}
          onSave={() =>
            save.mutate({
              armId: sheet.armId,
              termId: sheet.termId,
              version: sheet.version ?? null,
              rows: sheet.rows.map((row) => ({
                pupilId: row.pupilId,
                // A null value clears a rating (backend `IReadOnlyDictionary<string, string?>`); the generated contract loses
                // the value's nullability (STATE.md drift, 2026-09-22), hence the one widening assertion here.
                ratings: Object.fromEntries(
                  Object.entries(draft[row.pupilId] ?? {}).map(([trait, point]) => [trait, point || null]),
                ) as Record<string, string>,
              })),
            })
          }
        />
      ) : null}
    </div>
  );
}
