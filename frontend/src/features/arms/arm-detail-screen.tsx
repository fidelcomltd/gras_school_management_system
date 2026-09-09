import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useArm, useDeleteArm } from './api';
import { EditArmDialog } from './components/edit-arm-dialog';
import { FormTeacherLabel } from './components/form-teacher-label';
import { useFormTeacherNames } from './hooks/use-form-teacher-names';

/**
 * `/arms/:id` (spec 6.4.5, 6.4.7). Four required states (CONVENTIONS.md §11)
 * for the `GET /arms/{id}` fetch, mirroring `SessionDetailScreen`'s shape.
 * Roster, subjects in effect, result-set states and the transfer log are out
 * of scope (task card) — no pupils, enrolments, subject mappings or results
 * exist in this codebase yet.
 */
export function ArmDetailScreen() {
  // `:id` is always present when this route matches (`arms-routes.tsx`).
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const arm = useArm(id);
  const me = useMe();
  const deleteArm = useDeleteArm();
  const [showEdit, setShowEdit] = useState(false);

  const canUpdate = !!me.data && hasPrivilege(me.data, 'arm.update');
  const canDelete = !!me.data && hasPrivilege(me.data, 'arm.delete');
  const canAssignFormTeacher = !!me.data && hasPrivilege(me.data, 'arm.formteacher.assign');
  const canResolveFormTeacherNames = !!me.data && hasPrivilege(me.data, 'admin.view');

  const formTeacherNames = useFormTeacherNames(
    [arm.data?.formTeacherAdminId ?? null],
    canResolveFormTeacherNames,
  );

  if (arm.isPending) {
    return <output className="text-sm text-muted-foreground">Loading arm…</output>;
  }

  if (arm.isError) {
    if (arm.error instanceof ApiError && arm.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{arm.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void arm.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const detail = arm.data;
  const deleteError = deleteArm.error instanceof ApiError ? deleteArm.error.message : null;
  const currentFormTeacherName = detail.formTeacherAdminId
    ? formTeacherNames[detail.formTeacherAdminId]
    : undefined;

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">{detail.displayName}</h1>
          <p className="text-sm text-muted-foreground">{detail.classLevel}</p>
        </div>
        {canUpdate ? (
          <Button variant="outline" size="sm" onClick={() => setShowEdit(true)}>
            Edit
          </Button>
        ) : null}
      </header>

      {deleteError ? (
        <p role="alert" className="text-sm text-destructive">
          {deleteError}
        </p>
      ) : null}

      <dl className="flex flex-col gap-3">
        <div className="flex flex-col gap-0.5">
          <dt className="text-xs font-medium text-muted-foreground">Capacity</dt>
          <dd className="text-sm text-foreground">{detail.capacity}</dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="text-xs font-medium text-muted-foreground">Status</dt>
          <dd className="text-sm text-foreground">{detail.status}</dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="text-xs font-medium text-muted-foreground">Form teacher</dt>
          <dd className="text-sm">
            <FormTeacherLabel
              formTeacherAdminId={detail.formTeacherAdminId}
              name={currentFormTeacherName}
              canResolve={canResolveFormTeacherNames}
            />
          </dd>
        </div>
      </dl>

      {canDelete ? (
        <Button
          variant="destructive"
          size="sm"
          className="self-start"
          onClick={() => {
            if (window.confirm(`Delete ${detail.displayName}? This cannot be undone.`)) {
              deleteArm.mutate(detail.id, { onSuccess: () => navigate(paths.arms) });
            }
          }}
        >
          Delete arm
        </Button>
      ) : null}

      {showEdit ? (
        <EditArmDialog
          arm={detail}
          canAssignFormTeacher={canAssignFormTeacher}
          canResolveFormTeacherNames={canResolveFormTeacherNames}
          currentFormTeacherName={currentFormTeacherName}
          onClose={() => setShowEdit(false)}
        />
      ) : null}
    </div>
  );
}
