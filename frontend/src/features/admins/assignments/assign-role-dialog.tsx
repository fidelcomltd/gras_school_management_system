import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { LoadMoreButton } from '@/components/ui/load-more-button';
import { useArms } from '@/features/arms/api';
import { useRoles } from '@/features/roles/api';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useCreateAssignment } from './api';

type Scope = 'SchoolWide' | 'ArmList';

/**
 * Grants `staffName` a role for one session (spec 6.1.5): over the whole school (needs `role.assign`) or over chosen classes
 * (needs `role.scope.assign`), offering only the scopes the caller may grant. The seeded Super Admin role is never offered:
 * that is the account's own flag, set on Edit. Rules 1 and 3 are the server's; its refusal is shown in its own words.
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
  const roles = (rolesQuery.data?.pages.flatMap((page) => page.items) ?? []).filter((role) => !role.isSystem);
  const sessions = sessionsQuery.data?.pages.flatMap((page) => page.items) ?? [];

  const [roleId, setRoleId] = useState('');
  const [sessionChoice, setSessionChoice] = useState<string | null>(null);
  const [scope, setScope] = useState<Scope>(canSchoolWide ? 'SchoolWide' : 'ArmList');
  const [armIds, setArmIds] = useState<string[]>([]);

  const sessionId = sessionChoice ?? sessions.find((session) => session.state === 'Active')?.id ?? sessions[0]?.id ?? '';
  const armsQuery = useArms({ sessionId, status: 'Active' });
  const arms = sessionId ? (armsQuery.data?.pages.flatMap((page) => page.items) ?? []) : [];

  const ready = roleId !== '' && sessionId !== '' && (scope === 'SchoolWide' || armIds.length > 0);
  const formError = create.error instanceof ApiError ? create.error.message : null;

  const toggleArm = (id: string) => setArmIds((current) => (current.includes(id) ? current.filter((arm) => arm !== id) : [...current, id]));

  const submit = () =>
    create.mutate({ roleId, sessionId, scopeType: scope, armIds: scope === 'ArmList' ? armIds : [] }, { onSuccess: onClose });

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
            if (ready) submit();
          }}
        >
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <LabelledSelect
            label="Role"
            placeholder="Choose a role"
            value={roleId}
            options={roles.map((role) => ({ value: role.id, label: role.name }))}
            onChange={setRoleId}
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

          {scope === 'ArmList' ? (
            <fieldset className="flex max-h-56 flex-col gap-1.5 overflow-y-auto rounded-md border border-border p-3">
              <legend className="px-1 text-sm font-medium text-foreground">Classes</legend>
              {arms.length === 0 ? <p className="text-sm text-muted-foreground">This session has no active classes.</p> : null}
              {arms.map((arm) => (
                <label key={arm.id} className="flex items-center gap-2 text-sm text-foreground">
                  <input type="checkbox" checked={armIds.includes(arm.id)} onChange={() => toggleArm(arm.id)} />
                  {arm.displayName}
                </label>
              ))}
              {armsQuery.hasNextPage ? (
                <LoadMoreButton loading={armsQuery.isFetchingNextPage} onClick={() => void armsQuery.fetchNextPage()} />
              ) : null}
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
