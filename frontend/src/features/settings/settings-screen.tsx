import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useSettings } from './api';
import { SchoolIdentityForm } from './components/school-identity-form';
import type { SettingsIdentityGroupDto } from './types';

const IDENTITY_UPDATE_PRIVILEGE = 'settings.identity.update';

/**
 * `/settings` — mounted inside `ProtectedLayout`'s `<Outlet/>`, behind
 * `RequirePrivilege privilege="settings.view"` (route already gates entry).
 *
 * Four required states (CONVENTIONS.md §11) for the `GET /settings` fetch:
 * loading, error, unauthorized, and success below. No distinct "empty" state —
 * like `LandingScreen`'s `me` fetch, `settings` is a single-object read that
 * is either present on success or absent as one of the other three.
 */
export function SettingsScreen() {
  const settings = useSettings();
  const me = useMe();
  const [savedAt, setSavedAt] = useState<number | null>(null);

  if (settings.isPending) {
    return <output className="text-sm text-muted-foreground">Loading settings…</output>;
  }

  if (settings.isError) {
    if (settings.error instanceof ApiError && settings.error.kind === 'unauthorized') {
      // The route is already gated by `ProtectedLayout`; a 401 here means the
      // session just ended mid-flight. `ProtectedLayout`'s own subscription
      // is already navigating away — nothing to render here.
      return null;
    }
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{settings.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void settings.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const { identity } = settings.data;
  const canEdit = !!me.data && hasPrivilege(me.data, IDENTITY_UPDATE_PRIVILEGE);

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">School settings</h1>
        <p className="text-sm text-muted-foreground">
          The school&apos;s identity, as printed on result sheets and pin slips.
        </p>
      </header>

      {savedAt !== null ? (
        <output className="rounded-md bg-success-subtle px-3 py-2 text-sm text-success">
          Settings updated.
        </output>
      ) : null}

      {canEdit ? (
        <SchoolIdentityForm identity={identity} onSaved={() => setSavedAt(Date.now())} />
      ) : (
        <ReadOnlyIdentity identity={identity} />
      )}
    </div>
  );
}

/** Read-only view for a caller who can see settings but lacks `settings.identity.update`. */
function ReadOnlyIdentity({ identity }: { identity: SettingsIdentityGroupDto }) {
  const rows: [string, string][] = [
    ['School name', identity.schoolName],
    ['Short name', identity.shortName],
    ['Address', identity.address],
    ['Phone', identity.phone],
    ['Email', identity.email],
    ['Motto', identity.motto ?? '—'],
    ['Head teacher', identity.headTeacherName],
  ];

  return (
    <dl className="flex flex-col gap-3">
      {rows.map(([label, value]) => (
        <div key={label} className="flex flex-col gap-0.5">
          <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
          <dd className="text-sm text-foreground">{value}</dd>
        </div>
      ))}
    </dl>
  );
}
