import type { components } from '@/api/schema';
import { Button } from '@/components/ui/button';

type S = components['schemas'];

export interface IndicatorRow {
  key: string;
  id: string | null;
  name: string;
  status: S['DevelopmentIndicatorStatus'];
}

export interface DomainRow {
  key: string;
  id: string | null;
  sectionId: string;
  name: string;
  ratingScaleId: string;
  allowsIndicatorComment: boolean;
  status: S['DevelopmentDomainStatus'];
  indicators: IndicatorRow[];
}

const cell = 'h-9 rounded-md border border-input bg-background px-2 text-sm';

/** One development domain and its indicators (spec 6.2.13, Appendix E.3). Indicators with ratings are archived, not removed. */
export function DomainCard({
  domain,
  sections,
  scales,
  canEdit,
  onChange,
  onRemove,
}: {
  domain: DomainRow;
  sections: S['SectionDto'][];
  scales: S['RatingScaleDto'][];
  canEdit: boolean;
  onChange: (change: (domain: DomainRow) => DomainRow) => void;
  onRemove: () => void;
}) {
  const setIndicator = (key: string, change: Partial<IndicatorRow>) =>
    onChange((d) => ({ ...d, indicators: d.indicators.map((i) => (i.key === key ? { ...i, ...change } : i)) }));

  return (
    <fieldset className="flex flex-col gap-2 rounded-md border border-border p-3" disabled={!canEdit}>
      <legend className="sr-only">{domain.name || 'New domain'}</legend>
      <div className="flex flex-wrap items-center gap-2">
        <input aria-label="Domain name" className={`${cell} w-64`} value={domain.name} onChange={(e) => onChange((d) => ({ ...d, name: e.target.value }))} />
        <select aria-label={`Section for ${domain.name || 'new domain'}`} className={cell} value={domain.sectionId} onChange={(e) => onChange((d) => ({ ...d, sectionId: e.target.value }))}>
          <option value="">Section</option>
          {sections.map((section) => (
            <option key={section.id} value={section.id}>{section.name}</option>
          ))}
        </select>
        <select aria-label={`Scale for ${domain.name || 'new domain'}`} className={cell} value={domain.ratingScaleId} onChange={(e) => onChange((d) => ({ ...d, ratingScaleId: e.target.value }))}>
          <option value="">Scale</option>
          {scales.map((scale) => (
            <option key={scale.id} value={scale.id}>{scale.name}</option>
          ))}
        </select>
        <label className="flex items-center gap-1 text-sm">
          <input type="checkbox" checked={domain.allowsIndicatorComment} onChange={(e) => onChange((d) => ({ ...d, allowsIndicatorComment: e.target.checked }))} />
          Comments
        </label>
        <label className="flex items-center gap-1 text-sm text-muted-foreground">
          <input type="checkbox" checked={domain.status === 'Archived'} onChange={(e) => onChange((d) => ({ ...d, status: e.target.checked ? 'Archived' : 'Active' }))} />
          Archived
        </label>
        {canEdit && domain.id === null ? (
          <Button size="sm" variant="ghost" onClick={onRemove}>
            Remove domain
          </Button>
        ) : null}
      </div>
      {domain.indicators.map((indicator) => (
        <div key={indicator.key} className="flex items-center gap-2 pl-4">
          <input aria-label={`${domain.name} indicator`} className={`${cell} w-64`} value={indicator.name} onChange={(e) => setIndicator(indicator.key, { name: e.target.value })} />
          <label className="flex items-center gap-1 text-sm text-muted-foreground">
            <input type="checkbox" checked={indicator.status === 'Archived'} onChange={(e) => setIndicator(indicator.key, { status: e.target.checked ? 'Archived' : 'Active' })} />
            Archived
          </label>
          {canEdit && indicator.id === null ? (
            <Button size="sm" variant="ghost" onClick={() => onChange((d) => ({ ...d, indicators: d.indicators.filter((i) => i.key !== indicator.key) }))}>
              Remove
            </Button>
          ) : null}
        </div>
      ))}
      {canEdit ? (
        <div className="pl-4">
          <Button size="sm" variant="outline" onClick={() => onChange((d) => ({ ...d, indicators: [...d.indicators, { key: crypto.randomUUID(), id: null, name: '', status: 'Active' }] }))}>
            Add indicator
          </Button>
        </div>
      ) : null}
    </fieldset>
  );
}
