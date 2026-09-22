import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { usePupils } from './api';
import { CreatePupilDialog } from './components/create-pupil-dialog';
import { PUPIL_STATUSES, pupilName, type PupilsFilters } from './types';

const ALL = 'all';
const STATUS_ITEMS = [{ value: ALL, label: 'All statuses' }, ...PUPIL_STATUSES.map((status) => ({ value: status, label: status }))];

/** `/pupils` — the pupil register (spec 6.5.12): search by name or registration number, filter by status. */
export function PupilsListScreen() {
  const [filters, setFilters] = useState<PupilsFilters>({ search: '', status: '' });
  const [draft, setDraft] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const pupils = usePupils(filters);
  const me = useMe();
  const navigate = useNavigate();
  const canCreate = !!me.data && hasPrivilege(me.data, 'pupil.create');

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Pupils</h1>
          <p className="text-sm text-muted-foreground">Every pupil on the register, current and past.</p>
        </div>
        {canCreate ? <Button onClick={() => setShowCreate(true)}>New pupil</Button> : null}
      </header>

      <search>
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            setFilters((current) => ({ ...current, search: draft }));
          }}
        >
          <Input
            aria-label="Search by name or registration number"
            placeholder="Name or registration number"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            className="max-w-xs"
          />
          <Button type="submit" variant="outline">
            Search
          </Button>
          <div className="w-44">
            <Select
              items={STATUS_ITEMS}
              value={filters.status || ALL}
              onValueChange={(next) =>
                setFilters((current) => ({ ...current, status: !next || next === ALL ? '' : (next as PupilsFilters['status']) }))
              }
            >
              <SelectTrigger aria-label="Status" />
              <SelectContent>
                {STATUS_ITEMS.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </form>
      </search>

      {pupils.isPending ? (
        <LoadingState label="Loading pupils…" />
      ) : pupils.isError ? (
        <QueryErrorState error={pupils.error} onRetry={() => void pupils.refetch()} />
      ) : (
        <>
          {pupils.data.pages[0]?.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {filters.search || filters.status ? 'No pupils match these filters.' : 'No pupils yet.'}
            </p>
          ) : (
            <ul className="flex flex-col gap-2">
              {pupils.data.pages
                .flatMap((page) => page.items)
                .map((pupil) => (
                  <li key={pupil.id}>
                    <Link
                      to={paths.pupilDetail(pupil.id)}
                      className="flex flex-wrap items-center justify-between gap-4 rounded-md border border-border bg-surface px-4 py-3 text-sm hover:bg-muted"
                    >
                      <span className="font-medium text-foreground">{pupilName(pupil)}</span>
                      <span className="text-muted-foreground">
                        {pupil.registrationNumber ?? 'No number yet'} · {pupil.status}
                      </span>
                    </Link>
                  </li>
                ))}
            </ul>
          )}

          {pupils.hasNextPage ? (
            <Button variant="outline" size="sm" onClick={() => void pupils.fetchNextPage()} disabled={pupils.isFetchingNextPage}>
              {pupils.isFetchingNextPage ? 'Loading…' : 'Load more'}
            </Button>
          ) : null}
        </>
      )}

      {showCreate ? (
        <CreatePupilDialog onClose={() => setShowCreate(false)} onCreated={(pupil) => void navigate(paths.pupilDetail(pupil.id))} />
      ) : null}
    </div>
  );
}
