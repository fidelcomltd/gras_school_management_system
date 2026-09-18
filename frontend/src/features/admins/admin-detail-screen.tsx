import { useState } from 'react';
import { useParams } from 'react-router';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useAdmin, useRevokeAdminSessions } from './api';
import { ChangeStatusDialog } from './components/change-status-dialog';
import { EditAdminDialog } from './components/edit-admin-dialog';
import { ResetPasswordDialog } from './components/reset-password-dialog';
import type { AdminAccountStatus } from './types';

/** The status an account of each current status could legally move to. */
const STATUS_TARGETS: Record<AdminAccountStatus, AdminAccountStatus[]> = {
  Active: ['Suspended', 'Deactivated'],
  Suspended: ['Active', 'Deactivated'],
  Deactivated: ['Active'],
};

/**
 * `/admins/:id` (spec 6.1.8, 6.1.10, 6.1.11, 6.1.14). Four required states
 * (CONVENTIONS.md §11) for the `GET /admins/{id}` fetch, mirroring
 * `SessionDetailScreen`'s own shape.
 */
export function AdminDetailScreen() {
  // `:id` is always present when this route matches (`admins-routes.tsx`).
  const { id = '' } = useParams<{ id: string }>();
  const admin = useAdmin(id);
  const me = useMe();
  const revokeSessions = useRevokeAdminSessions();
  const [showEdit, setShowEdit] = useState(false);
  const [showStatus, setShowStatus] = useState(false);
  const [showReset, setShowReset] = useState(false);

  if (admin.isPending) {
    return <output className="text-sm text-muted-foreground">Loading admin account…</output>;
  }

  if (admin.isError) {
    if (admin.error instanceof ApiError && admin.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{admin.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void admin.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const detail = admin.data;
  // A caller can never change their OWN status (spec/added rule, ASSUMPTIONS.md §2.16).
  const isSelf = me.data?.accountId === detail.id;
  const canEdit = !!me.data && hasPrivilege(me.data, 'admin.update');
  const canGrantSuperAdmin = !!me.data?.isSuperAdmin;
  const canResetPassword = !!me.data && hasPrivilege(me.data, 'admin.password.reset');
  const canRevokeSessions = !!me.data && hasPrivilege(me.data, 'admin.session.revoke');

  const statusTargets =
    isSelf || !me.data
      ? []
      : STATUS_TARGETS[detail.status].filter((target) => {
          if (target === 'Deactivated') return hasPrivilege(me.data, 'admin.deactivate');
          if (detail.status === 'Deactivated') {
            return hasPrivilege(me.data, 'admin.deactivate') && me.data.isSuperAdmin;
          }
          return hasPrivilege(me.data, 'admin.suspend');
        });

  const revokeError = revokeSessions.error instanceof ApiError ? revokeSessions.error.message : null;

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">{detail.staffName}</h1>
          <p className="text-sm text-muted-foreground">{detail.email}</p>
        </div>
        {canEdit ? (
          <Button variant="outline" size="sm" onClick={() => setShowEdit(true)}>
            Edit
          </Button>
        ) : null}
      </header>

      <dl className="flex flex-col gap-3">
        {(
          [
            ['Phone', detail.phone ?? '—'],
            ['Status', detail.status],
            ['Super Admin', detail.isSuperAdmin ? 'Yes' : 'No'],
            ['Must change password', detail.mustChangePassword ? 'Yes' : 'No'],
            ['Last signed in', detail.lastLoginAtUtc ?? 'Never'],
            ['Created', detail.createdAtUtc],
          ] as const
        ).map(([label, value]) => (
          <div key={label} className="flex flex-col gap-0.5">
            <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
            <dd className="text-sm text-foreground">{value}</dd>
          </div>
        ))}
      </dl>

      {revokeError ? (
        <p role="alert" className="text-sm text-destructive">
          {revokeError}
        </p>
      ) : null}

      <div className="flex flex-wrap gap-2">
        {statusTargets.length > 0 ? (
          <Button variant="outline" size="sm" onClick={() => setShowStatus(true)}>
            Change status
          </Button>
        ) : null}
        {canResetPassword ? (
          <Button variant="outline" size="sm" onClick={() => setShowReset(true)}>
            Reset password
          </Button>
        ) : null}
        {canRevokeSessions ? (
          <Button
            variant="destructive"
            size="sm"
            disabled={revokeSessions.isPending}
            onClick={() => {
              if (
                window.confirm(
                  `Revoke every active session for ${detail.staffName}? They will be signed out everywhere.`,
                )
              ) {
                revokeSessions.mutate(detail.id);
              }
            }}
          >
            {revokeSessions.isPending ? 'Revoking…' : 'Revoke sessions'}
          </Button>
        ) : null}
      </div>

      {showEdit ? (
        <EditAdminDialog admin={detail} canGrantSuperAdmin={canGrantSuperAdmin} onClose={() => setShowEdit(false)} />
      ) : null}
      {showStatus ? (
        <ChangeStatusDialog admin={detail} targets={statusTargets} onClose={() => setShowStatus(false)} />
      ) : null}
      {showReset ? <ResetPasswordDialog admin={detail} onClose={() => setShowReset(false)} /> : null}
    </div>
  );
}
