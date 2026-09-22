import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useUpdateDevelopmentDomains } from '../api-ratings';
import { DomainCard, type DomainRow } from './domain-card';
import { ReasonField } from './reason-field';

type S = components['schemas'];

/**
 * Nursery development domains and their indicators (spec 6.2.13, Appendix E.3), per section. Saved whole; order is as
 * listed. Keyed by version by its parent.
 */
export function DevelopmentDomainsEditor({
  group,
  sections,
  scales,
  canEdit,
}: {
  group: S['SettingsDevelopmentDomainGroupDto'];
  sections: S['SectionDto'][];
  scales: S['RatingScaleDto'][];
  canEdit: boolean;
}) {
  const save = useUpdateDevelopmentDomains();
  const [domains, setDomains] = useState<DomainRow[]>(() =>
    [...group.domains]
      .sort((a, b) => Number(a.displayOrder) - Number(b.displayOrder))
      .map((d) => ({
        key: d.id,
        id: d.id,
        sectionId: d.sectionId,
        name: d.name,
        ratingScaleId: d.ratingScaleId,
        allowsIndicatorComment: d.allowsIndicatorComment,
        status: d.status,
        indicators: [...d.indicators]
          .sort((a, b) => Number(a.displayOrder) - Number(b.displayOrder))
          .map((i) => ({ key: i.id, id: i.id, name: i.name, status: i.status })),
      })),
  );
  const [reason, setReason] = useState('');

  return (
    <section aria-label="Development domains" className="flex flex-col gap-3">
      <h2 className="text-sm font-semibold text-foreground">Nursery development domains</h2>
      {domains.map((domain) => (
        <DomainCard
          key={domain.key}
          domain={domain}
          sections={sections}
          scales={scales}
          canEdit={canEdit}
          onChange={(change) => setDomains((current) => current.map((d) => (d.key === domain.key ? change(d) : d)))}
          onRemove={() => setDomains((current) => current.filter((d) => d.key !== domain.key))}
        />
      ))}
      {canEdit ? (
        <>
          <div>
            <Button
              size="sm"
              variant="outline"
              onClick={() =>
                setDomains((current) => [
                  ...current,
                  { key: crypto.randomUUID(), id: null, sectionId: '', name: '', ratingScaleId: '', allowsIndicatorComment: false, status: 'Active', indicators: [] },
                ])
              }
            >
              Add domain
            </Button>
          </div>
          <ReasonField value={reason} onChange={setReason} />
          <FormError message={save.error instanceof ApiError ? save.error.message : null} />
          <div>
            <Button
              disabled={save.isPending}
              onClick={() =>
                save.mutate({
                  domains: domains.map((d, index) => ({
                    id: d.id,
                    sectionId: d.sectionId,
                    name: d.name.trim(),
                    displayOrder: index + 1,
                    ratingScaleId: d.ratingScaleId,
                    allowsIndicatorComment: d.allowsIndicatorComment,
                    status: d.status,
                    indicators: d.indicators.map((i, order) => ({ id: i.id, name: i.name.trim(), displayOrder: order + 1, status: i.status })),
                  })),
                  expectedVersion: group.versionNumber,
                  reason: reason.trim() || null,
                })
              }
            >
              {save.isPending ? 'Saving…' : 'Save development domains'}
            </Button>
          </div>
        </>
      ) : null}
    </section>
  );
}
