import { useState } from 'react';
import type { components } from '@/api/schema';
import { useSaveDevelopmentRatings } from '../api-records';
import { isEditable } from '../types';
import { LockNotice, SaveBar } from './save-bar';

type DevelopmentRatingSheetDto = components['schemas']['DevelopmentRatingSheetDto'];
interface Cell {
  pointId: string;
  comment: string;
}
type Draft = Record<string, Record<string, Cell>>;

const WARN_ABOVE = 60;

/**
 * Nursery development ratings (spec 6.7.7, Appendix E.3): one select per indicator, with a comment where the domain
 * allows one. Shown per domain so a wide scale still fits. Keyed by version by its parent.
 */
export function DevelopmentRatingsEditor({ sheet, canEdit }: { sheet: DevelopmentRatingSheetDto; canEdit: boolean }) {
  const save = useSaveDevelopmentRatings(sheet.armId, sheet.termId);
  const [initial] = useState<Draft>(() =>
    Object.fromEntries(
      sheet.rows.map((row) => [
        row.pupilId,
        Object.fromEntries(Object.entries(row.ratings).map(([indicator, rating]) => [indicator, { pointId: rating?.pointId ?? '', comment: rating?.comment ?? '' }])),
      ]),
    ),
  );
  const [draft, setDraft] = useState(initial);
  const state = sheet.resultSet?.state;
  const editable = canEdit && isEditable(state);
  const set = (pupilId: string, indicatorId: string, change: Partial<Cell>) =>
    setDraft((current) => {
      const cell = current[pupilId]?.[indicatorId] ?? { pointId: '', comment: '' };
      return { ...current, [pupilId]: { ...current[pupilId], [indicatorId]: { ...cell, ...change } } };
    });

  if (sheet.rows.length === 0) return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;

  return (
    <div className="flex flex-col gap-5">
      {!isEditable(state) ? <LockNotice state={state} what="ratings" /> : null}
      {Number(sheet.activeIndicatorTotal) > WARN_ABOVE ? (
        <p className="text-sm text-muted-foreground">
          There are {sheet.activeIndicatorTotal} indicators to rate for each pupil; the printed sheet may run to a second page.
        </p>
      ) : null}
      {sheet.domains.map((domain) => (
        <section key={domain.id} aria-label={domain.name} className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold text-foreground">{domain.name}</h2>
          <p className="text-xs text-muted-foreground">{domain.scale.points.map((p) => `${p.pointCode} = ${p.pointLabel}`).join(' · ')}</p>
          <div className="overflow-x-auto rounded-md border border-border">
            <table className="w-full text-sm">
              <thead className="bg-muted text-muted-foreground">
                <tr>
                  <th scope="col" className="px-3 py-2 text-left font-medium">Pupil</th>
                  {domain.indicators.map((indicator) => (
                    <th key={indicator.id} scope="col" className="px-2 py-2 font-medium">{indicator.name}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {sheet.rows.map((row) => (
                  <tr key={row.pupilId} className="border-t border-border align-top">
                    <th scope="row" className="px-3 py-1.5 text-left font-normal text-foreground">{row.displayName}</th>
                    {domain.indicators.map((indicator) => {
                      const cell = draft[row.pupilId]?.[indicator.id] ?? { pointId: '', comment: '' };
                      return (
                        <td key={indicator.id} className="px-2 py-1.5">
                          <div className="flex flex-col items-center gap-1">
                            <select
                              aria-label={`${indicator.name} for ${row.displayName}`}
                              className="h-8 rounded-md border border-input bg-background px-1 text-sm"
                              disabled={!editable}
                              value={cell.pointId}
                              onChange={(event) => set(row.pupilId, indicator.id, { pointId: event.target.value })}
                            >
                              <option value="">–</option>
                              {domain.scale.points.map((point) => (
                                <option key={point.id} value={point.id}>{point.pointCode}</option>
                              ))}
                            </select>
                            {domain.allowsIndicatorComment ? (
                              <input
                                aria-label={`Comment on ${indicator.name} for ${row.displayName}`}
                                className="h-8 w-32 rounded-md border border-input bg-background px-2 text-xs"
                                placeholder="Comment"
                                disabled={!editable || !cell.pointId}
                                value={cell.comment}
                                onChange={(event) => set(row.pupilId, indicator.id, { comment: event.target.value })}
                              />
                            ) : null}
                          </div>
                        </td>
                      );
                    })}
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
                ratings: Object.fromEntries(
                  Object.entries(draft[row.pupilId] ?? {}).map(([indicator, cell]) => [
                    indicator,
                    // `pointId: null` clears the whole cell, point and comment (contract description, Q3-A).
                    cell.pointId ? { pointId: cell.pointId, comment: cell.comment.trim() || null } : { pointId: null, comment: null },
                  ]),
                ),
              })),
            })
          }
        />
      ) : null}
    </div>
  );
}
