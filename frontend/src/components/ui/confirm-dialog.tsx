import { useState, type ReactNode } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { normalizeError } from '@/lib/http';
import { Button } from './button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from './dialog';

/**
 * The app's confirmation step, in place of the browser's `window.confirm` (lead, 2026-09-28: never the native box).
 * Render it only while confirming, as the feature dialogs are. `onConfirm` returns the mutation's promise
 * (`mutateAsync`): the dialog stays open, busy, until it settles, closes on success, and on failure shows the server's
 * message inside itself so the user sees why and can retry or cancel.
 */
export function ConfirmDialog({
  title,
  description,
  confirmLabel,
  pendingLabel,
  variant = 'destructive',
  onConfirm,
  onClose,
}: {
  title: string;
  description: ReactNode;
  confirmLabel: string;
  pendingLabel: string;
  variant?: 'destructive' | 'primary';
  onConfirm: () => Promise<unknown>;
  onClose: () => void;
}) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function confirm(): Promise<void> {
    setPending(true);
    setError(null);
    try {
      await onConfirm();
      onClose();
    } catch (caught) {
      setError(normalizeError(caught).message);
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent showCloseButton={!pending}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <FormError message={error} />
        <DialogFooter>
          <Button type="button" variant="ghost" disabled={pending} onClick={onClose}>
            Cancel
          </Button>
          <Button type="button" variant={variant} disabled={pending} onClick={() => void confirm()}>
            {pending ? pendingLabel : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
