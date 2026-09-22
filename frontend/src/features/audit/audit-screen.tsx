import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useAuditEvents, useExportAudit, type AuditFilters } from './api';
import { AuditRow } from './audit-row';

const EMPTY: AuditFilters = { from: '', to: '', action: '', entityType: '', outcome: '' };
const control = 'h-10 rounded-md border border-input bg-background px-2 text-sm';

/** `/audit` — who changed what, when (spec 6.1.10), with filters and a CSV export. */
export function AuditScreen() {
  const [draft, setDraft] = useState<AuditFilters>(EMPTY);
  const [filters, setFilters] = useState<AuditFilters>(EMPTY);
  const events = useAuditEvents(filters);
  const exportCsv = useExportAudit();
  const me = useMe();
  const canExport = !!me.data && hasPrivilege(me.data, 'audit.export');
  const set = (change: Partial<AuditFilters>) => setDraft((current) => ({ ...current, ...change }));

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Audit log</h1>
        <p className="text-sm text-muted-foreground">Every change and every refused attempt, newest first. Times are Lagos time.</p>
      </header>

      <search>
        <form
          className="flex flex-wrap items-end gap-3 text-sm"
          onSubmit={(event) => {
            event.preventDefault();
            setFilters(draft);
          }}
        >
          <label className="flex flex-col gap-1">
            From
            <input type="date" className={control} value={draft.from} onChange={(e) => set({ from: e.target.value })} />
          </label>
          <label className="flex flex-col gap-1">
            To
            <input type="date" className={control} value={draft.to} onChange={(e) => set({ to: e.target.value })} />
          </label>
          <Input aria-label="Action, e.g. result.publish" placeholder="Action, e.g. result.publish" className="w-56" value={draft.action} onChange={(e) => set({ action: e.target.value })} />
          <Input aria-label="Record type, e.g. result_set" placeholder="Record type, e.g. result_set" className="w-52" value={draft.entityType} onChange={(e) => set({ entityType: e.target.value })} />
          <label className="flex flex-col gap-1">
            Outcome
            <select className={control} value={draft.outcome} onChange={(e) => set({ outcome: e.target.value as AuditFilters['outcome'] })}>
              <option value="">Any</option>
              <option value="Success">Succeeded</option>
              <option value="Rejected">Refused</option>
            </select>
          </label>
          <Button type="submit" variant="outline">
            Filter
          </Button>
          {canExport ? (
            <Button type="button" variant="ghost" disabled={exportCsv.isPending} onClick={() => exportCsv.mutate(filters)}>
              {exportCsv.isPending ? 'Exporting…' : 'Export CSV'}
            </Button>
          ) : null}
        </form>
      </search>
      <FormError message={exportCsv.error instanceof ApiError ? exportCsv.error.message : null} />

      {events.isPending ? (
        <LoadingState label="Loading the audit log…" />
      ) : events.isError ? (
        <QueryErrorState error={events.error} onRetry={() => void events.refetch()} />
      ) : events.data.pages[0]?.items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No events match these filters.</p>
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {events.data.pages.flatMap((page) => page.items).map((event) => (
              <AuditRow key={event.id} event={event} />
            ))}
          </ul>
          {events.hasNextPage ? (
            <div>
              <Button variant="outline" size="sm" disabled={events.isFetchingNextPage} onClick={() => void events.fetchNextPage()}>
                {events.isFetchingNextPage ? 'Loading…' : 'Load more'}
              </Button>
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}
