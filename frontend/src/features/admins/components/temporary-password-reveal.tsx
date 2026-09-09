import { useState } from 'react';
import { Button } from '@/components/ui/button';

/**
 * The one-time credential reveal (TASK-0043), shared by `CreateAdminDialog`
 * and `ResetPasswordDialog`. By the time this renders, the caller has already
 * copied the password out of the mutation's result into its own local state
 * and called the mutation's `reset()` — so nothing here re-reads a cache, and
 * closing this (however the dialog is dismissed) is the only way the value
 * stops existing anywhere in this app's memory.
 */
export function TemporaryPasswordReveal({ password, onDone }: { password: string; onDone: () => void }) {
  const [copied, setCopied] = useState(false);

  function copy(): void {
    const clipboard = navigator.clipboard;
    if (!clipboard) return;
    clipboard
      .writeText(password)
      .then(() => setCopied(true))
      .catch(() => {
        /* Clipboard access denied — the password is still visible to copy by hand. */
      });
  }

  return (
    <div className="flex flex-col gap-4">
      <p role="alert" className="rounded-md bg-warning-subtle px-3 py-2 text-sm text-warning-foreground">
        This password will not be shown again. Copy it now and share it securely.
      </p>
      <div className="flex items-center gap-2">
        <code className="flex-1 truncate rounded-md border border-border bg-surface-sunken px-3 py-2 text-sm text-foreground">
          {password}
        </code>
        <Button type="button" variant="outline" size="sm" onClick={copy}>
          {copied ? 'Copied' : 'Copy'}
        </Button>
      </div>
      <Button type="button" onClick={onDone}>
        Done
      </Button>
    </div>
  );
}
