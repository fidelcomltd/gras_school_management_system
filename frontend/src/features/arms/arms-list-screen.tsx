import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import type { ArmsFilters } from './api';
import { ArmFilters, type ArmFilterState } from './components/arm-filters';
import { ArmList } from './components/arm-list';
import { BulkCreateArmsDialog } from './components/bulk-create-arms-dialog';
import { CreateArmDialog } from './components/create-arm-dialog';

const EMPTY_FILTERS: ArmFilterState = { sessionId: '', levelId: '', status: '', search: '' };

/**
 * `/arms` — the rooms per session (spec 6.4.5), gated `arm.view` at the
 * route (`arms-routes.tsx`). `search` maps onto the wire's `label` query
 * param, which matches by substring against the composed `displayName`.
 */
export function ArmsListScreen() {
  const me = useMe();
  const [filters, setFilters] = useState<ArmFilterState>(EMPTY_FILTERS);
  const [showCreate, setShowCreate] = useState(false);
  const [showBulkCreate, setShowBulkCreate] = useState(false);

  const canCreate = !!me.data && hasPrivilege(me.data, 'arm.create');

  const queryFilters: ArmsFilters = {
    ...(filters.sessionId ? { sessionId: filters.sessionId } : {}),
    ...(filters.levelId ? { levelId: filters.levelId } : {}),
    ...(filters.status ? { status: filters.status } : {}),
    ...(filters.search ? { label: filters.search } : {}),
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Arms</h1>
          <p className="text-sm text-muted-foreground">The rooms within each level, and who runs them.</p>
        </div>
        {canCreate ? (
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setShowBulkCreate(true)}>
              Create arms for session
            </Button>
            <Button onClick={() => setShowCreate(true)}>New arm</Button>
          </div>
        ) : null}
      </header>

      <ArmFilters value={filters} onChange={setFilters} />

      <ArmList filters={queryFilters} />

      {showCreate ? <CreateArmDialog onClose={() => setShowCreate(false)} /> : null}
      {showBulkCreate ? <BulkCreateArmsDialog onClose={() => setShowBulkCreate(false)} /> : null}
    </div>
  );
}
