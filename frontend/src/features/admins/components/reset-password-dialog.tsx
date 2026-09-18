import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';
import { useResetAdminPassword } from '../api';
import type { AdminAccountDetailDto } from '../types';
import { TemporaryPasswordReveal } from './temporary-password-reveal';

/**
 * `POST /api/v1/admins/{id}/password-reset` (spec 6.1.11): mints a new
 * temporary password, revokes every active session. Same one-time reveal
 * treatment as `CreateAdminDialog` — copied to local state, then the
 * mutation is `reset()` immediately so it does not linger in the query cache.
 */
export function ResetPasswordDialog({ admin, onClose }: { admin: AdminAccountDetailDto; onClose: () => void }) {
  const resetPassword = useResetAdminPassword();
  const [password, setPassword] = useState<string | null>(null);

  const formError = resetPassword.error instanceof ApiError ? resetPassword.error.message : null;

  function submit(): void {
    resetPassword.mutate(admin.id, {
      onSuccess: (result) => {
        setPassword(result.temporaryPassword ?? '');
        resetPassword.reset();
      },
    });
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {password ? `Temporary password for ${admin.staffName}` : `Reset password for ${admin.staffName}?`}
          </DialogTitle>
        </DialogHeader>

        {password ? (
          <TemporaryPasswordReveal password={password} onDone={onClose} />
        ) : (
          <div className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              This mints a new temporary password for {admin.staffName} and signs them out of every active
              session.
            </p>
            {formError ? (
              <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {formError}
              </p>
            ) : null}
            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button disabled={resetPassword.isPending} onClick={submit}>
                {resetPassword.isPending ? 'Resetting…' : 'Reset password'}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
