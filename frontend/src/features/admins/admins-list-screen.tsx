import { ShieldUser } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useAdmins } from './api';
import { lagosDateTime } from '@/shared/format/date';
import { CopyAssignmentsDialog } from './assignments/copy-assignments-dialog';
import { CreateAdminDialog } from './components/create-admin-dialog';
import type { AdminAccountStatus } from './types';
import { LoadingState } from '@/components/feedback/query-states';
import { LoadMoreButton } from '@/components/ui/load-more-button';
import { EmptyState } from '@/components/feedback/empty-state';

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
  const [showCopy, setShowCopy] = useState(false);
  const canCreate = !!me.data && hasPrivilege(me.data, 'admin.create');
  const canCopy = !!me.data && hasPrivilege(me.data, 'role.assign');

  if (admins.isPending) {
    return <LoadingState label="Loading admin accounts…" />;
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
        <div className="flex flex-wrap gap-2">
          {canCopy ? (
            <Button variant="outline" onClick={() => setShowCopy(true)}>
              Copy assignments…
            </Button>
          ) : null}
          {canCreate ? <Button onClick={() => setShowCreate(true)}>New admin</Button> : null}
        </div>
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
        <EmptyState icon={ShieldUser} title="No admin accounts found." />
      ) : (
        // Spec 6.1.8's columns: who can do what is the question this list answers, so roles and scope are columns.
        <div className="overflow-x-auto rounded-md border border-border">
          <table aria-label="Admin accounts" className="w-full text-left text-sm">
            <thead className="bg-surface-sunken text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">Staff</th>
                <th className="px-3 py-2 font-medium">Roles held</th>
                <th className="px-3 py-2 font-medium">Scope</th>
                <th className="px-3 py-2 font-medium">Status</th>
                <th className="px-3 py-2 font-medium">Last login</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border bg-surface">
              {items.map((admin) => (
                <tr key={admin.id}>
                  <td className="px-3 py-2">
                    <Link to={paths.adminDetail(admin.id)} aria-label={admin.staffName} className="flex flex-col hover:underline">
                      <span className="font-medium text-foreground">{admin.staffName}</span>
                      <span className="text-xs text-muted-foreground">{admin.email}</span>
                    </Link>
                  </td>
                  <td className="px-3 py-2 text-foreground">
                    {admin.rolesHeld.length === 0 ? <span className="text-muted-foreground">No roles yet</span> : admin.rolesHeld.join(', ')}
                  </td>
                  <td className="px-3 py-2 text-muted-foreground">{admin.scopeSummary}</td>
                  <td className="px-3 py-2 text-muted-foreground">{admin.status}</td>
                  <td className="px-3 py-2 text-muted-foreground">{admin.lastLoginAtUtc ? lagosDateTime(admin.lastLoginAtUtc) : 'Never'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {admins.hasNextPage ? (
        <LoadMoreButton loading={admins.isFetchingNextPage} onClick={() => void admins.fetchNextPage()} />
      ) : null}

      {showCreate ? <CreateAdminDialog onClose={() => setShowCreate(false)} /> : null}
      {showCopy ? <CopyAssignmentsDialog onClose={() => setShowCopy(false)} /> : null}
    </div>
  );
}
