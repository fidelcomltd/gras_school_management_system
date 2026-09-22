import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useUpdateTraits } from '../api-ratings';
import { ReasonField } from './reason-field';

type S = components['schemas'];
type Domain = S['TraitDomain'];
interface Row {
  key: string;
  id: string | null;
  domain: Domain;
  name: string;
  status: S['TraitStatus'];
}

const cell = 'h-9 rounded-md border border-input bg-background px-2 text-sm';
const DOMAINS: { domain: Domain; label: string; scaleField: 'affectiveRatingScaleId' | 'psychomotorRatingScaleId' }[] = [
  { domain: 'Affective', label: 'Affective domain', scaleField: 'affectiveRatingScaleId' },
  { domain: 'Psychomotor', label: 'Psychomotor skills', scaleField: 'psychomotorRatingScaleId' },
];

/**
 * The traits a primary class is rated on (spec 6.2.7, Appendix F.3), and the scale each block uses. A trait with ratings
 * is archived rather than removed, so old sheets still print it. Order is as listed. Keyed by version by its parent.
 */
export function TraitsEditor({ group, scales, canEdit }: { group: S['SettingsTraitsGroupDto']; scales: S['RatingScaleDto'][]; canEdit: boolean }) {
  const save = useUpdateTraits();
  const [rows, setRows] = useState<Row[]>(() =>
    [...group.traits].sort((a, b) => Number(a.displayOrder) - Number(b.displayOrder)).map((t) => ({ key: t.id, id: t.id, domain: t.domain, name: t.name, status: t.status })),
  );
  const [scaleIds, setScaleIds] = useState({ affectiveRatingScaleId: group.affectiveRatingScaleId, psychomotorRatingScaleId: group.psychomotorRatingScaleId });
  const [reason, setReason] = useState('');
  const set = (key: string, change: Partial<Row>) => setRows((current) => current.map((r) => (r.key === key ? { ...r, ...change } : r)));

  return (
    <section aria-label="Traits" className="flex flex-col gap-3">
      <h2 className="text-sm font-semibold text-foreground">Primary traits</h2>
      {DOMAINS.map(({ domain, label, scaleField }) => (
        <fieldset key={domain} className="flex flex-col gap-2 rounded-md border border-border p-3" disabled={!canEdit}>
          <legend className="px-1 text-sm font-medium text-foreground">{label}</legend>
          <label className="flex items-center gap-2 text-sm">
            Rated on
            <select className={cell} value={scaleIds[scaleField] ?? ''} onChange={(e) => setScaleIds((current) => ({ ...current, [scaleField]: e.target.value }))}>
              <option value="">Choose a scale</option>
              {scales.map((scale) => (
                <option key={scale.id} value={scale.id}>{scale.name}</option>
              ))}
            </select>
          </label>
          {rows.filter((r) => r.domain === domain).map((row) => (
            <div key={row.key} className="flex items-center gap-2">
              <input aria-label={`${label} trait name`} className={`${cell} w-56`} value={row.name} onChange={(e) => set(row.key, { name: e.target.value })} />
              <label className="flex items-center gap-1 text-sm text-muted-foreground">
                <input type="checkbox" checked={row.status === 'Archived'} onChange={(e) => set(row.key, { status: e.target.checked ? 'Archived' : 'Active' })} />
                Archived
              </label>
              {canEdit && row.id === null ? (
                <Button size="sm" variant="ghost" onClick={() => setRows((current) => current.filter((r) => r.key !== row.key))}>
                  Remove
                </Button>
              ) : null}
            </div>
          ))}
          {canEdit ? (
            <div>
              <Button size="sm" variant="outline" onClick={() => setRows((current) => [...current, { key: crypto.randomUUID(), id: null, domain, name: '', status: 'Active' }])}>
                Add trait
              </Button>
            </div>
          ) : null}
        </fieldset>
      ))}
      {canEdit ? (
        <>
          <ReasonField value={reason} onChange={setReason} />
          <FormError message={save.error instanceof ApiError ? save.error.message : null} />
          <div>
            <Button
              disabled={save.isPending || !scaleIds.affectiveRatingScaleId || !scaleIds.psychomotorRatingScaleId}
              onClick={() =>
                save.mutate({
                  affectiveRatingScaleId: scaleIds.affectiveRatingScaleId ?? '',
                  psychomotorRatingScaleId: scaleIds.psychomotorRatingScaleId ?? '',
                  traits: DOMAINS.flatMap(({ domain }) =>
                    rows.filter((r) => r.domain === domain).map((r, index) => ({ id: r.id, domain, name: r.name.trim(), displayOrder: index + 1, status: r.status })),
                  ),
                  expectedVersion: group.versionNumber,
                  reason: reason.trim() || null,
                })
              }
            >
              {save.isPending ? 'Saving…' : 'Save traits'}
            </Button>
          </div>
        </>
      ) : null}
    </section>
  );
}
