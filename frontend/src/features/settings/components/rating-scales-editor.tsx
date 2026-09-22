import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useUpdateRatingScales } from '../api-ratings';
import { ReasonField } from './reason-field';

type Group = components['schemas']['SettingsRatingScaleGroupDto'];
interface Point {
  key: string;
  id: string | null;
  pointCode: string;
  pointLabel: string;
}
interface Scale {
  key: string;
  id: string | null;
  name: string;
  points: Point[];
}

const cell = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

/**
 * The rating scales traits and development domains are rated on (spec 6.2.13), e.g. E/S/I/N. Points are ordered as
 * listed. A scale still used by a block cannot be removed (the server says which). Keyed by version by its parent.
 */
export function RatingScalesEditor({ group, canEdit }: { group: Group; canEdit: boolean }) {
  const save = useUpdateRatingScales();
  const [scales, setScales] = useState<Scale[]>(() =>
    group.scales.map((s) => ({ key: s.id, id: s.id, name: s.name, points: s.points.map((p) => ({ key: p.id, id: p.id, pointCode: p.pointCode, pointLabel: p.pointLabel })) })),
  );
  const [reason, setReason] = useState('');
  const setScale = (key: string, change: (scale: Scale) => Scale) => setScales((current) => current.map((s) => (s.key === key ? change(s) : s)));

  return (
    <section aria-label="Rating scales" className="flex flex-col gap-3">
      <h2 className="text-sm font-semibold text-foreground">Rating scales</h2>
      {scales.map((scale) => (
        <fieldset key={scale.key} className="flex flex-col gap-2 rounded-md border border-border p-3" disabled={!canEdit}>
          <legend className="sr-only">{scale.name || 'New scale'}</legend>
          <div className="flex items-center gap-2">
            <input aria-label="Scale name" className={`${cell} w-56`} value={scale.name} onChange={(e) => setScale(scale.key, (s) => ({ ...s, name: e.target.value }))} />
            {canEdit ? (
              <Button size="sm" variant="ghost" onClick={() => setScales((current) => current.filter((s) => s.key !== scale.key))}>
                Remove scale
              </Button>
            ) : null}
          </div>
          {scale.points.map((point, index) => (
            <div key={point.key} className="flex items-center gap-2 pl-4">
              <input aria-label={`${scale.name} point ${index + 1} code`} className={`${cell} w-14`} value={point.pointCode} onChange={(e) => setScale(scale.key, (s) => ({ ...s, points: s.points.map((p) => (p.key === point.key ? { ...p, pointCode: e.target.value } : p)) }))} />
              <input aria-label={`${scale.name} point ${index + 1} meaning`} className={`${cell} w-48`} value={point.pointLabel} onChange={(e) => setScale(scale.key, (s) => ({ ...s, points: s.points.map((p) => (p.key === point.key ? { ...p, pointLabel: e.target.value } : p)) }))} />
              {canEdit ? (
                <Button size="sm" variant="ghost" onClick={() => setScale(scale.key, (s) => ({ ...s, points: s.points.filter((p) => p.key !== point.key) }))}>
                  Remove <span className="sr-only">point {point.pointCode}</span>
                </Button>
              ) : null}
            </div>
          ))}
          {canEdit ? (
            <div className="pl-4">
              <Button size="sm" variant="outline" onClick={() => setScale(scale.key, (s) => ({ ...s, points: [...s.points, { key: crypto.randomUUID(), id: null, pointCode: '', pointLabel: '' }] }))}>
                Add point
              </Button>
            </div>
          ) : null}
        </fieldset>
      ))}
      {canEdit ? (
        <>
          <div>
            <Button size="sm" variant="outline" onClick={() => setScales((current) => [...current, { key: crypto.randomUUID(), id: null, name: '', points: [] }])}>
              Add scale
            </Button>
          </div>
          <ReasonField value={reason} onChange={setReason} />
          <FormError message={save.error instanceof ApiError ? save.error.message : null} />
          <div>
            <Button
              disabled={save.isPending}
              onClick={() =>
                save.mutate({
                  scales: scales.map((s) => ({
                    id: s.id,
                    name: s.name.trim(),
                    points: s.points.map((p, index) => ({ id: p.id, pointCode: p.pointCode.trim().toUpperCase(), pointLabel: p.pointLabel.trim(), pointOrder: index + 1 })),
                  })),
                  expectedVersion: group.versionNumber,
                  reason: reason.trim() || null,
                })
              }
            >
              {save.isPending ? 'Saving…' : 'Save rating scales'}
            </Button>
          </div>
        </>
      ) : null}
    </section>
  );
}
