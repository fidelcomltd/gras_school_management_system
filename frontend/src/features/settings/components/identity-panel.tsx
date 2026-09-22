import { useState } from 'react';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { SchoolIdentityForm } from './school-identity-form';
import { SchoolImageField } from './school-image-field';
import type { SettingsIdentityGroupDto } from '../types';

/** The school's identity (spec 6.2.3), with the logo and signature printed on sheets and slips. */
export function IdentityPanel({ identity }: { identity: SettingsIdentityGroupDto }) {
  const me = useMe();
  const [savedAt, setSavedAt] = useState<number | null>(null);
  const canEdit = !!me.data && hasPrivilege(me.data, 'settings.identity.update');

  return (
    <div className="flex max-w-xl flex-col gap-6">
      {savedAt !== null ? (
        <output className="rounded-md bg-success-subtle px-3 py-2 text-sm text-success">Settings updated.</output>
      ) : null}
      {canEdit ? (
        <SchoolIdentityForm identity={identity} onSaved={() => setSavedAt(Date.now())} />
      ) : (
        <ReadOnlyIdentity identity={identity} />
      )}
      <SchoolImageField kind="logo" image={identity.logo ?? null} canEdit={canEdit} />
      <SchoolImageField kind="signature" image={identity.signature ?? null} canEdit={canEdit} />
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
