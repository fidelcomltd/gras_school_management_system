import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';

const MAX = 500;

/**
 * A reasoned action (return or withdraw results; revoke or reinstate a pin). The server's length rule is mirrored by
 * `minLength` (10 for result transitions, 1 for pins) and a 500-character maximum.
 */
export function ReasonDialog({
  title,
  description,
  minLength = 10,
  action,
  pending,
  error,
  onSubmit,
  onClose,
}: {
  title: string;
  description: string;
  minLength?: number | undefined;
  action: string;
  pending: boolean;
  error: Error | null;
  onSubmit: (reason: string) => void;
  onClose: () => void;
}) {
  const [reason, setReason] = useState('');
  const length = reason.trim().length;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            if (length >= minLength && length <= MAX) onSubmit(reason.trim());
          }}
        >
          <FormError message={error instanceof ApiError ? error.message : null} />
          <label htmlFor="transition-reason" className="text-sm font-medium text-foreground">
            {minLength > 1 ? `Reason (${minLength} to ${MAX} characters)` : 'Reason'}
          </label>
          <textarea
            id="transition-reason"
            className="min-h-24 rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={length < minLength || length > MAX || pending}>
              {pending ? 'Working…' : action}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
