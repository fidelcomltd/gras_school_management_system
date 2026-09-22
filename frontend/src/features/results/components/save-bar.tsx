import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { STATE_LABEL, type ResultSetState } from '../types';

/** Why a record sheet is read-only, when it is. */
export function LockNotice({ state, what }: { state: ResultSetState | undefined; what: string }) {
  return state ? (
    <p className="text-sm text-muted-foreground">
      This class's results are {STATE_LABEL[state].toLowerCase()}, so {what} can no longer change.
    </p>
  ) : null;
}

/** Save button, status line and server error for a whole-sheet editor. */
export function SaveBar({
  label,
  dirty,
  invalidMessage,
  pending,
  error,
  onSave,
}: {
  label: string;
  dirty: boolean;
  invalidMessage?: string | null | undefined;
  pending: boolean;
  error: Error | null;
  onSave: () => void;
}) {
  return (
    <div className="flex flex-col gap-2">
      <FormError message={error instanceof ApiError ? error.message : null} />
      <div className="flex items-center gap-3">
        <Button disabled={!dirty || !!invalidMessage || pending} onClick={onSave}>
          {pending ? 'Saving…' : label}
        </Button>
        <output className="text-sm text-muted-foreground">{invalidMessage ?? (dirty ? 'Unsaved changes.' : 'All changes saved.')}</output>
      </div>
    </div>
  );
}
