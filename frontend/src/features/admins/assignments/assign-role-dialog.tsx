import { useState } from 'react';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useArms } from '@/features/arms/api';
import { usePrivileges, useRoles } from '@/features/roles/api';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { useAllPages } from '@/lib/query/use-all-pages';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useCreateAssignment } from './api';

type Scope = 'SchoolWide' | 'ArmList';

/**
 * Grants `staffName` a role for one session (spec 6.1.5): over the whole school (needs `role.assign`) or over chosen classes
 * (needs `role.scope.assign`), offering only the scopes the caller may grant. For chosen classes, only roles made entirely of
 * scopable privileges are offered, since spec 4.2 refuses any other there. The seeded Super Admin role is never offered: that
 * is the account's own flag, set on Edit. Rules 1 and 3 are the server's; its refusal is shown in its own words.
 */
export function AssignRoleDialog({
  adminId,
  staffName,
  canSchoolWide,
  canScoped,
  onClose,
}: {
  adminId: string;
  staffName: string;
  canSchoolWide: boolean;
  canScoped: boolean;
  onClose: () => void;
}) {
  const create = useCreateAssignment(adminId);
  const rolesQuery = useRoles();
  const sessionsQuery = useSessions();
  const privilegesQuery = usePrivileges();
  const allRoles = useAllPages(rolesQuery).filter((role) => !role.isSystem);
  const sessions = useAllPages(sessionsQuery);

  const [roleChoice, setRoleChoice] = useState('');
  const [sessionChoice, setSessionChoice] = useState<string | null>(null);
  const [scope, setScope] = useState<Scope>(canSchoolWide ? 'SchoolWide' : 'ArmList');
  const [armIds, setArmIds] = useState<string[]>([]);

  const sessionId = sessionChoice ?? sessions.find((session) => session.state === 'Active')?.id ?? sessions[0]?.id ?? '';
  const armsQuery = useArms({ sessionId, status: 'Active' }, sessionId !== '' && scope === 'ArmList');
  const arms = useAllPages(armsQuery, sessionId !== '' && scope === 'ArmList');

  const scopable = new Set(
    (privilegesQuery.data?.groups ?? []).flatMap((group) => group.privileges).filter((privilege) => privilege.scopable).map((privilege) => privilege.code),
  );
  const roles = scope === 'ArmList' ? allRoles.filter((role) => role.privileges.every((code) => scopable.has(code))) : allRoles;
  // A role chosen for the whole school that cannot be scoped drops out when the scope changes.
  const roleId = roles.some((role) => role.id === roleChoice) ? roleChoice : '';

  const ready = roleId !== '' && sessionId !== '' && (scope === 'SchoolWide' || armIds.length > 0);
  const formError = create.error instanceof ApiError ? create.error.message : null;
  const toggleArm = (id: string) => setArmIds((current) => (current.includes(id) ? current.filter((arm) => arm !== id) : [...current, id]));

  const pickers = () => {
    if (rolesQuery.isError) return <QueryErrorState error={rolesQuery.error} onRetry={() => void rolesQuery.refetch()} />;
    if (sessionsQuery.isError) return <QueryErrorState error={sessionsQuery.error} onRetry={() => void sessionsQuery.refetch()} />;
    if (rolesQuery.isPending || sessionsQuery.isPending || (scope === 'ArmList' && privilegesQuery.isPending)) {
      return <LoadingState label="Loading roles and sessions…" />;
    }
    return (
      <>
        <LabelledSelect
          label="Role"
          placeholder={roles.length === 0 ? 'No role can be assigned here' : 'Choose a role'}
          value={roleId}
          options={roles.map((role) => ({ value: role.id, label: role.name }))}
          onChange={setRoleChoice}
          className="w-full"
        />
        <LabelledSelect
          label="Session"
          placeholder="Session"
          value={sessionId}
          options={sessions.map((session) => ({ value: session.id, label: session.name }))}
          onChange={(next) => {
            setSessionChoice(next);
            setArmIds([]);
          }}
          className="w-full"
        />
      </>
    );
  };

  const classes = () => {
    if (armsQuery.isError) return <QueryErrorState error={armsQuery.error} onRetry={() => void armsQuery.refetch()} />;
    if (armsQuery.isPending || armsQuery.hasNextPage) return <LoadingState label="Loading classes…" />;
    if (arms.length === 0) return <p className="text-sm text-muted-foreground">This session has no active classes.</p>;
    return arms.map((arm) => (
      <label key={arm.id} className="flex items-center gap-2 text-sm text-foreground">
        <input type="checkbox" checked={armIds.includes(arm.id)} onChange={() => toggleArm(arm.id)} />
        {arm.displayName}
      </label>
    ));
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Assign a role to {staffName}</DialogTitle>
        </DialogHeader>

        <form
          className="flex flex-col gap-4"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            if (ready) {
              create.mutate({ roleId, sessionId, scopeType: scope, armIds: scope === 'ArmList' ? armIds : [] }, { onSuccess: onClose });
            }
          }}
        >
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 text-sm font-medium text-foreground">Where it applies</legend>
            {canSchoolWide ? (
              <label className="flex items-center gap-2 text-sm text-foreground">
                <input type="radio" name="scope" checked={scope === 'SchoolWide'} onChange={() => setScope('SchoolWide')} />
                The whole school
              </label>
            ) : null}
            {canScoped ? (
              <label className="flex items-center gap-2 text-sm text-foreground">
                <input type="radio" name="scope" checked={scope === 'ArmList'} onChange={() => setScope('ArmList')} />
                Chosen classes only
              </label>
            ) : null}
          </fieldset>

          {pickers()}

          {scope === 'ArmList' && sessionId ? (
            <fieldset className="flex max-h-56 flex-col gap-1.5 overflow-y-auto rounded-md border border-border p-3">
              <legend className="px-1 text-sm font-medium text-foreground">Classes</legend>
              {classes()}
            </fieldset>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={!ready || create.isPending}>
              {create.isPending ? 'Assigning…' : 'Assign role'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
