import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';

const MIN = 10;
const MAX = 500;

/** A reasoned transition (return for correction, withdraw a publication): 10 to 500 characters, as the server requires. */
export function ReasonDialog({
  title,
  description,
  action,
  pending,
  error,
  onSubmit,
  onClose,
}: {
  title: string;
  description: string;
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
            if (length >= MIN && length <= MAX) onSubmit(reason.trim());
          }}
        >
          <FormError message={error instanceof ApiError ? error.message : null} />
          <label htmlFor="transition-reason" className="text-sm font-medium text-foreground">
            Reason ({MIN} to {MAX} characters)
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
            <Button type="submit" disabled={length < MIN || length > MAX || pending}>
              {pending ? 'Working…' : action}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
