import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { useArms } from '@/features/arms/api';
import { useRoles } from '@/features/roles/api';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { useAllPages } from '@/lib/query/use-all-pages';
import { useAssignments, useRevokeAssignment } from './api';
import { AssignRoleDialog } from './assign-role-dialog';
import type { RoleAssignmentDto } from './types';

/** An arm-scoped assignment's classes by name, from every page of its session's arms. */
function ArmNames({ assignment }: { assignment: RoleAssignmentDto }) {
  const enabled = assignment.sessionId !== null;
  const query = useArms({ sessionId: assignment.sessionId ?? '' }, enabled);
  const arms = useAllPages(query, enabled);
  if (query.isPending || query.hasNextPage) return <>loading classes…</>;
  const names = assignment.armIds.map((id) => arms.find((arm) => arm.id === id)?.displayName ?? 'a removed class');
  return <>{names.join(', ')}</>;
}

/**
 * The account's roles (spec 6.1.5, 6.1.14): what they hold, for which session, over the whole school or chosen classes, and
 * assign or revoke. Never on your own account (escalation rule 1): the buttons are absent there, and the server refuses anyway.
 * Names come from the roles and sessions lists; a viewer without `role.view` sees that the name is hidden, not a wrong one.
 */
export function AssignmentsSection({
  adminId,
  staffName,
  isSelf,
  canAssign,
  canScopeAssign,
}: {
  adminId: string;
  staffName: string;
  isSelf: boolean;
  canAssign: boolean;
  canScopeAssign: boolean;
}) {
  const assignments = useAssignments(adminId);
  const revoke = useRevokeAssignment(adminId);
  const activeQuery = useRoles();
  const archivedQuery = useRoles('Archived');
  const sessionsQuery = useSessions();
  const roles = [...useAllPages(activeQuery), ...useAllPages(archivedQuery)];
  const sessions = useAllPages(sessionsQuery);
  const [assigning, setAssigning] = useState(false);

  const roleName = (id: string) =>
    roles.find((role) => role.id === id)?.name ?? (activeQuery.isError ? 'Role (name needs role.view)' : 'Unknown role');
  const sessionName = (id: string | null) =>
    id === null ? 'Every session' : (sessions.find((session) => session.id === id)?.name ?? 'Unknown session');
  const describe = (assignment: RoleAssignmentDto) => `${roleName(assignment.roleId)}, ${sessionName(assignment.sessionId)}`;

  const canGrant = !isSelf && (canAssign || canScopeAssign);
  const revokeError = revoke.error instanceof ApiError ? revoke.error.message : null;

  const body = () => {
    if (assignments.isPending) return <LoadingState label="Loading roles…" />;
    if (assignments.isError) return <QueryErrorState error={assignments.error} onRetry={() => void assignments.refetch()} />;
    const current = assignments.data.filter((assignment) => assignment.status === 'Active');
    const revoked = assignments.data.length - current.length;
    return (
      <div className="flex flex-col gap-2">
        {current.length === 0 ? (
          <p className="text-sm text-muted-foreground">No roles yet. Until one is assigned, this account can sign in but do nothing.</p>
        ) : (
          <ul className="flex flex-col divide-y divide-border rounded-md border border-border">
            {current.map((assignment) => (
              <li key={assignment.id} className="flex flex-wrap items-center justify-between gap-3 px-3 py-2">
                <div className="flex flex-col">
                  <span className="text-sm font-medium text-foreground">{roleName(assignment.roleId)}</span>
                  <span className="text-xs text-muted-foreground">
                    {sessionName(assignment.sessionId)} ·{' '}
                    {assignment.scopeType === 'SchoolWide' ? 'Whole school' : <ArmNames assignment={assignment} />}
                  </span>
                </div>
                {canAssign && !isSelf ? (
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={revoke.isPending}
                    aria-label={`Revoke ${describe(assignment)}`}
                    onClick={() => {
                      if (window.confirm(`Revoke ${describe(assignment)} from ${staffName}? They lose its privileges at once.`)) {
                        revoke.mutate(assignment.id);
                      }
                    }}
                  >
                    Revoke
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        )}
        {revoked > 0 ? (
          <p className="text-xs text-muted-foreground">
            {revoked} revoked {revoked === 1 ? 'assignment is' : 'assignments are'} kept for the record and not shown.
          </p>
        ) : null}
      </div>
    );
  };

  return (
    <section aria-labelledby="admin-roles" className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <h2 id="admin-roles" className="text-lg font-semibold text-foreground">
          Roles
        </h2>
        {canGrant ? (
          <Button size="sm" onClick={() => setAssigning(true)}>
            Assign role
          </Button>
        ) : null}
      </div>
      {isSelf ? <p className="text-sm text-muted-foreground">Your own roles are changed by another Super Admin.</p> : null}
      {revokeError ? (
        <p role="alert" className="text-sm text-destructive">
          {revokeError}
        </p>
      ) : null}
      {body()}
      {assigning ? (
        <AssignRoleDialog
          adminId={adminId}
          staffName={staffName}
          canSchoolWide={canAssign}
          canScoped={canScopeAssign}
          onClose={() => setAssigning(false)}
        />
      ) : null}
    </section>
  );
}
