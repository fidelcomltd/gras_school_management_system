import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useAdmins } from './api';
import { CreateAdminDialog } from './components/create-admin-dialog';
import type { AdminAccountStatus } from './types';

const STATUS_OPTIONS: { value: AdminAccountStatus | ''; label: string }[] = [
  { value: '', label: 'Active & suspended' },
  { value: 'Active', label: 'Active' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Deactivated', label: 'Deactivated' },
];

/**
 * `/admins` — who else has access (spec 6.1.8). Four required states
 * (CONVENTIONS.md §11) for the `GET /admins` fetch: loading, empty, error,
 * unauthorized, mirroring `SessionsListScreen`'s own shape.
 */
export function AdminsListScreen() {
  const [status, setStatus] = useState<AdminAccountStatus | ''>('');
  const admins = useAdmins(status === '' ? undefined : status);
  const me = useMe();
  const [showCreate, setShowCreate] = useState(false);
  const canCreate = !!me.data && hasPrivilege(me.data, 'admin.create');

  if (admins.isPending) {
    return <output className="text-sm text-muted-foreground">Loading admin accounts…</output>;
  }

  if (admins.isError) {
    if (admins.error instanceof ApiError && admins.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{admins.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void admins.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const items = admins.data.pages.flatMap((page) => page.items);

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Admin accounts</h1>
          <p className="text-sm text-muted-foreground">Who else has access, and what they can do.</p>
        </div>
        {canCreate ? <Button onClick={() => setShowCreate(true)}>New admin</Button> : null}
      </header>

      <label className="flex items-center gap-2 text-sm text-foreground">
        Status
        <select
          className="h-9 rounded-md border border-input bg-surface px-2 text-sm text-foreground"
          value={status}
          onChange={(e) => setStatus(e.target.value as AdminAccountStatus | '')}
        >
          {STATUS_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </label>

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No admin accounts found.</p>
      ) : (
        <ul aria-label="Admin accounts" className="flex flex-col gap-2">
          {items.map((admin) => (
            <li key={admin.id}>
              <Link
                to={paths.adminDetail(admin.id)}
                className="flex items-center justify-between gap-4 rounded-md border border-border bg-surface px-4 py-3 text-sm hover:bg-muted"
              >
                <span className="flex flex-col">
                  <span className="font-medium text-foreground">{admin.staffName}</span>
                  <span className="text-xs text-muted-foreground">{admin.email}</span>
                </span>
                <span className="text-muted-foreground">{admin.status}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {admins.hasNextPage ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => void admins.fetchNextPage()}
          disabled={admins.isFetchingNextPage}
        >
          {admins.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      ) : null}

      {showCreate ? <CreateAdminDialog onClose={() => setShowCreate(false)} /> : null}
    </div>
  );
}
