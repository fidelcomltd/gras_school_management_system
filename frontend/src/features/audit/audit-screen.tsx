import { ScrollText } from 'lucide-react';
import { useRef, useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useAuditEvents, useExportAudit, type AuditFilters } from './api';
import { AuditRow } from './audit-row';
import { LoadMoreButton } from '@/components/ui/load-more-button';
import { EmptyState } from '@/components/feedback/empty-state';

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
  // Filters apply as they change (lead, 2026-09-28: waiting for a quiet "Filter" button read as broken). A date or the
  // outcome applies at once; typed text after a short pause, or at once on Enter.
  // Every apply takes the whole draft, so choosing a date also applies text still waiting on its pause.
  const typing = useRef<number | undefined>(undefined);
  const latest = useRef<AuditFilters>(EMPTY);
  const edit = (change: Partial<AuditFilters>) => {
    latest.current = { ...latest.current, ...change };
    setDraft(latest.current);
  };
  const applyNow = (change: Partial<AuditFilters>) => {
    window.clearTimeout(typing.current);
    edit(change);
    setFilters(latest.current);
  };
  const applySoon = (change: Partial<AuditFilters>) => {
    edit(change);
    window.clearTimeout(typing.current);
    typing.current = window.setTimeout(() => setFilters(latest.current), 400);
  };
  const filtered = JSON.stringify(filters) !== JSON.stringify(EMPTY);

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
            window.clearTimeout(typing.current);
            setFilters(latest.current);
          }}
        >
          <label className="flex flex-col gap-1">
            From
            <input type="date" className={control} value={draft.from} onChange={(e) => applyNow({ from: e.target.value })} />
          </label>
          <label className="flex flex-col gap-1">
            To
            <input type="date" className={control} value={draft.to} onChange={(e) => applyNow({ to: e.target.value })} />
          </label>
          <Input aria-label="Action" placeholder="Action, e.g. publish" className="w-56" value={draft.action} onChange={(e) => applySoon({ action: e.target.value })} />
          <Input aria-label="Record type" placeholder="Record type, e.g. result" className="w-52" value={draft.entityType} onChange={(e) => applySoon({ entityType: e.target.value })} />
          <label className="flex flex-col gap-1">
            Outcome
            <select className={control} value={draft.outcome} onChange={(e) => applyNow({ outcome: e.target.value as AuditFilters['outcome'] })}>
              <option value="">Any</option>
              <option value="Success">Succeeded</option>
              <option value="Rejected">Refused</option>
            </select>
          </label>
          {filtered ? (
            <Button type="button" variant="ghost" onClick={() => applyNow(EMPTY)}>
              Clear filters
            </Button>
          ) : null}
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
        <EmptyState icon={ScrollText} title="No events match these filters." />
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {events.data.pages.flatMap((page) => page.items).map((event) => (
              <AuditRow key={event.id} event={event} />
            ))}
          </ul>
          {events.hasNextPage ? (
            <div>
              <LoadMoreButton loading={events.isFetchingNextPage} onClick={() => void events.fetchNextPage()} />
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}
