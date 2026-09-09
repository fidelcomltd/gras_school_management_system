import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useDeleteRole, useRoles } from './api';
import { CreateRoleDialog } from './components/create-role-dialog';
import { EditRoleDialog } from './components/edit-role-dialog';
import type { RoleDto } from './types';

/**
 * `/roles` (TASK-0028 §2). Role *assignments* and scopes are out of scope
 * (TASK-0030, blocked on arms — the backend does not exist yet); this screen
 * only manages the role definitions themselves: name, description,
 * privileges, active/archived.
 */
export function RolesListScreen() {
  const roles = useRoles();
  const me = useMe();
  const deleteRole = useDeleteRole();
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<RoleDto | null>(null);

  const canCreate = !!me.data && hasPrivilege(me.data, 'role.create');
  const canEdit = !!me.data && hasPrivilege(me.data, 'role.update');
  const canDelete = !!me.data && hasPrivilege(me.data, 'role.delete');

  if (roles.isPending) {
    return <output className="text-sm text-muted-foreground">Loading roles…</output>;
  }

  if (roles.isError) {
    if (roles.error instanceof ApiError && roles.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{roles.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void roles.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const items = roles.data.pages.flatMap((page) => page.items);
  const deleteError = deleteRole.error instanceof ApiError ? deleteRole.error.message : null;

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Roles</h1>
          <p className="text-sm text-muted-foreground">What each role is allowed to do.</p>
        </div>
        {canCreate ? <Button onClick={() => setShowCreate(true)}>New role</Button> : null}
      </header>

      {deleteError ? (
        <p role="alert" className="text-sm text-destructive">
          {deleteError}
        </p>
      ) : null}

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No roles yet.</p>
      ) : (
        <ul aria-label="Roles" className="flex flex-col gap-2">
          {items.map((role) => (
            <li
              key={role.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border bg-surface px-4 py-3 text-sm"
            >
              <div className="flex flex-col">
                <span className="font-medium text-foreground">
                  {role.name}
                  {role.isSystem ? ' · System' : ''}
                </span>
                <span className="text-xs text-muted-foreground">
                  {role.privileges.length} privilege{role.privileges.length === 1 ? '' : 's'} · {role.status}
                </span>
              </div>
              <div className="flex flex-wrap gap-2">
                {canEdit ? (
                  <Button variant="outline" size="sm" onClick={() => setEditing(role)}>
                    Edit
                  </Button>
                ) : null}
                {canDelete ? (
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => {
                      if (window.confirm(`Delete ${role.name}? This cannot be undone.`)) {
                        deleteRole.mutate(role.id);
                      }
                    }}
                  >
                    Delete
                  </Button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}

      {roles.hasNextPage ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => void roles.fetchNextPage()}
          disabled={roles.isFetchingNextPage}
        >
          {roles.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      ) : null}

      {showCreate ? <CreateRoleDialog onClose={() => setShowCreate(false)} /> : null}
      {editing ? <EditRoleDialog role={editing} onClose={() => setEditing(null)} /> : null}
    </div>
  );
}
