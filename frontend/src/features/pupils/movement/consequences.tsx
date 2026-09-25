import type { PupilMovementEffect, PupilMovementOutcome } from './api';

const EFFECT_TEXT: Record<PupilMovementEffect, string> = {
  Blocks: 'published: withdraw these results first',
  NeedsRecompute: 'will need computing again',
  RevertsToDraft: 'goes back to Draft and will need computing again',
};

/** What a dry run said the change will do (spec 06 §6.4.4 steps 5 and 6): result sets touched, and capacity. */
export function Consequences({ outcome }: { outcome: PupilMovementOutcome }) {
  const blocked = outcome.resultSets.some((set) => set.effect === 'Blocks');
  const capacity = outcome.capacity;

  return (
    <div className="flex flex-col gap-2 rounded-md border border-border bg-muted/40 p-3 text-sm" aria-live="polite">
      <p className="font-medium text-foreground">What this change does</p>
      {outcome.resultSets.length === 0 ? (
        <p className="text-muted-foreground">No result set is affected.</p>
      ) : (
        <ul className="flex flex-col gap-1">
          {outcome.resultSets.map((set) => (
            <li key={set.resultSetId} className={set.effect === 'Blocks' ? 'text-destructive' : 'text-foreground'}>
              {set.armName} {set.termName} results: {EFFECT_TEXT[set.effect] ?? set.effect}.
            </li>
          ))}
        </ul>
      )}
      {capacity?.overCapacity ? (
        <p className={capacity.canOverride ? 'text-foreground' : 'text-destructive'}>
          {outcome.toArmName} will have {capacity.enrolledAfter} pupils against a capacity of {capacity.capacity}.
          {capacity.canOverride ? ' Your override will be recorded.' : ' You cannot go over capacity; choose another class.'}
        </p>
      ) : null}
      {blocked ? (
        <p role="alert" className="text-destructive">
          This move is blocked until the published results above are withdrawn.
        </p>
      ) : null}
    </div>
  );
}
