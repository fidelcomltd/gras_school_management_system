import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useSections } from '../api';
import type { SectionDto } from '../types';
import { CreateSectionDialog } from './create-section-dialog';
import { EditSectionDialog } from './edit-section-dialog';

/**
 * Sections (spec 6.4.9): "a two-row seeded list a school extends rarely." Not
 * paged. Gated under `level.*` — the register has no `section.*` code (see
 * `types.ts`'s header comment and `.agent/STATE.md`'s TASK-0038 entry).
 */
export function SectionList() {
  const sections = useSections();
  const me = useMe();
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<SectionDto | null>(null);

  const canCreate = !!me.data && hasPrivilege(me.data, 'level.create');
  const canEdit = !!me.data && hasPrivilege(me.data, 'level.update');

  if (sections.isPending) {
    return <output className="text-sm text-muted-foreground">Loading sections…</output>;
  }

  if (sections.isError) {
    if (sections.error instanceof ApiError && sections.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{sections.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void sections.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const items = sections.data.sections;

  return (
    <div className="flex flex-col gap-4 pt-4">
      <div className="flex justify-end">
        {canCreate ? <Button size="sm" onClick={() => setShowCreate(true)}>New section</Button> : null}
      </div>

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No sections yet.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((section) => (
            <li
              key={section.id}
              className="flex items-center justify-between gap-3 rounded-md border border-border bg-surface px-4 py-3 text-sm"
            >
              <span className="font-medium text-foreground">{section.name}</span>
              {canEdit ? (
                <Button variant="outline" size="sm" onClick={() => setEditing(section)}>
                  Rename
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      )}

      {showCreate ? <CreateSectionDialog onClose={() => setShowCreate(false)} /> : null}
      {editing ? <EditSectionDialog section={editing} onClose={() => setEditing(null)} /> : null}
    </div>
  );
}
