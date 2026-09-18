import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useDeleteLevel, useLevels, useReorderLevels } from '../api';
import type { LevelDto } from '../types';
import { CreateLevelDialog } from './create-level-dialog';
import { EditLevelDialog } from './edit-level-dialog';

/**
 * Levels, in `progressionOrder` (spec 6.4.9) — never alphabetically (AC).
 * `?status=all` reveals inactive levels alongside active ones.
 *
 * Reorder ships as move-up/move-down buttons rather than drag-and-drop —
 * TASK-0042's own cut line, both posting the identical whole-ordered-array
 * `POST /levels/reorder` body the drag-and-drop version would.
 */
export function LevelList() {
  const [showAll, setShowAll] = useState(false);
  const levels = useLevels(showAll ? 'all' : undefined);
  const me = useMe();
  const reorder = useReorderLevels();
  const deleteLevel = useDeleteLevel();
  const [showCreate, setShowCreate] = useState(false);
  const [editing, setEditing] = useState<LevelDto | null>(null);

  const canCreate = !!me.data && hasPrivilege(me.data, 'level.create');
  const canEdit = !!me.data && hasPrivilege(me.data, 'level.update');
  const canDelete = !!me.data && hasPrivilege(me.data, 'level.delete');
  const canDeactivate = !!me.data && hasPrivilege(me.data, 'level.deactivate');

  if (levels.isPending) {
    return <output className="text-sm text-muted-foreground">Loading levels…</output>;
  }

  if (levels.isError) {
    if (levels.error instanceof ApiError && levels.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{levels.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void levels.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const items = [...levels.data.pages.flatMap((page) => page.items)].sort(
    (a, b) => Number(a.progressionOrder) - Number(b.progressionOrder),
  );

  function move(index: number, direction: -1 | 1): void {
    const next = [...items];
    const target = index + direction;
    const a = next[index];
    const b = next[target];
    if (!a || !b) return;
    next[index] = b;
    next[target] = a;
    reorder.mutate({ orderedLevelIds: next.map((level) => level.id) });
  }

  const deleteError = deleteLevel.error instanceof ApiError ? deleteLevel.error.message : null;

  return (
    <div className="flex flex-col gap-4 pt-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <label className="flex items-center gap-2 text-sm text-foreground">
          <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} />
          Show inactive levels too
        </label>
        {canCreate ? <Button size="sm" onClick={() => setShowCreate(true)}>New level</Button> : null}
      </div>

      {deleteError ? (
        <p role="alert" className="text-sm text-destructive">
          {deleteError}
        </p>
      ) : null}

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No levels yet.</p>
      ) : (
        <ul aria-label="Levels" className="flex flex-col gap-2">
          {items.map((level, index) => (
            <li
              key={level.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border bg-surface px-4 py-3 text-sm"
            >
              <div className="flex flex-col">
                <span className="font-medium text-foreground">
                  {level.name}
                  {level.isEntryLevel ? ' · Entry' : ''}
                  {level.isGraduatingLevel ? ' · Graduating' : ''}
                </span>
                <span className="text-xs text-muted-foreground">
                  {level.section} · order {level.progressionOrder} · {level.status}
                </span>
              </div>

              <div className="flex flex-wrap gap-2">
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Move ${level.name} up`}
                  disabled={index === 0 || reorder.isPending}
                  onClick={() => move(index, -1)}
                >
                  ↑
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Move ${level.name} down`}
                  disabled={index === items.length - 1 || reorder.isPending}
                  onClick={() => move(index, 1)}
                >
                  ↓
                </Button>
                {canEdit ? (
                  <Button variant="outline" size="sm" onClick={() => setEditing(level)}>
                    Edit
                  </Button>
                ) : null}
                {canDelete ? (
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={() => {
                      if (window.confirm(`Delete ${level.name}? This cannot be undone.`)) {
                        deleteLevel.mutate(level.id);
                      }
                    }}
                  >
                    Delete
                  </Button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}

      {levels.hasNextPage ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => void levels.fetchNextPage()}
          disabled={levels.isFetchingNextPage}
        >
          {levels.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      ) : null}

      {showCreate ? <CreateLevelDialog levels={items} onClose={() => setShowCreate(false)} /> : null}
      {editing ? (
        <EditLevelDialog
          levels={items}
          level={editing}
          canDeactivate={canDeactivate}
          onClose={() => setEditing(null)}
        />
      ) : null}
    </div>
  );
}
