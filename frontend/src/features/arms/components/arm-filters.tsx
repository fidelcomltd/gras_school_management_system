import { useLevels } from '@/features/classes/api';
import { useSessions } from '@/features/sessions/api';
import type { ArmStatus } from '../types';
import { ArmLevelSelect, ArmStatusSelect, SessionSelect } from './arm-pickers';

export interface ArmFilterState {
  sessionId: string;
  levelId: string;
  status: ArmStatus | '';
  search: string;
}

/**
 * Filter row for `ArmsListScreen` (spec 6.4.5): session, level, status, and
 * free text against the composed display name — `label` on the wire, which
 * `ListArms`'s own description says matches "substring against the composed
 * display name" (so typing `2c` finds Primary 2C). Form-teacher filtering by
 * name is out of scope here: it would need the same admin-name resolution
 * `FormTeacherLabel` does, for a filter control rather than a display, which
 * the task card's judgement call did not ask for.
 */
export function ArmFilters({
  value,
  onChange,
}: {
  value: ArmFilterState;
  onChange: (next: ArmFilterState) => void;
}) {
  const sessions = useSessions();
  const levels = useLevels();
  const sessionItems = sessions.data?.pages.flatMap((p) => p.items) ?? [];
  const levelItems = levels.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div className="flex flex-wrap items-end gap-3">
      <label className="flex flex-col gap-1 text-sm text-foreground">
        Search
        <input
          type="search"
          placeholder="e.g. 2c"
          value={value.search}
          onChange={(e) => onChange({ ...value, search: e.target.value })}
          className="h-9 w-40 rounded-md border border-input bg-surface px-2 text-sm text-foreground"
        />
      </label>

      <div className="flex flex-col gap-1">
        <span className="text-sm text-foreground">Session</span>
        <SessionSelect
          value={value.sessionId}
          onChange={(sessionId) => onChange({ ...value, sessionId })}
          sessions={sessionItems}
          label="Filter by session"
        />
      </div>

      <div className="flex flex-col gap-1">
        <span className="text-sm text-foreground">Level</span>
        <ArmLevelSelect
          value={value.levelId}
          onChange={(levelId) => onChange({ ...value, levelId })}
          levels={levelItems}
          label="Filter by level"
        />
      </div>

      <div className="flex flex-col gap-1">
        <span className="text-sm text-foreground">Status</span>
        <ArmStatusSelect
          value={value.status}
          onChange={(status) => onChange({ ...value, status })}
          label="Filter by status"
        />
      </div>
    </div>
  );
}
