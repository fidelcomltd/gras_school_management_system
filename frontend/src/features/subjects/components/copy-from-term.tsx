import { Button } from '@/components/ui/button';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';

/** Picks a source term (any session, usually last term) to copy mappings from (spec 6.6.8). */
export function CopyFromTerm({
  destinationTermId,
  busy,
  onPreview,
}: {
  destinationTermId: string;
  busy: boolean;
  onPreview: (sourceTermId: string) => void;
}) {
  const source = useTermChoice();
  const same = source.termId === destinationTermId;

  return (
    <fieldset className="flex flex-wrap items-end gap-3 rounded-md border border-border p-3">
      <legend className="px-1 text-sm font-medium text-foreground">Copy from another term</legend>
      <TermPicker choice={source} />
      <Button variant="outline" disabled={busy || !source.termId || same} onClick={() => onPreview(source.termId)}>
        Preview copy
      </Button>
      {same ? <p className="text-sm text-muted-foreground">Choose a different term to copy from.</p> : null}
    </fieldset>
  );
}
