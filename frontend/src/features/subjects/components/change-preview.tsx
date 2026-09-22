import { Button } from '@/components/ui/button';
import type { SaveSubjectMappingGridResponse } from '../types';

/** A dry run's additions and endings, with the confirm that applies them (spec 6.6.9). */
export function ChangePreview({
  preview,
  applying,
  onApply,
  onCancel,
}: {
  preview: SaveSubjectMappingGridResponse;
  applying: boolean;
  onApply: () => void;
  onCancel: () => void;
}) {
  const nothing = preview.additions.length === 0 && preview.endings.length === 0;

  return (
    <section aria-label="Changes to apply" className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4 text-sm">
      {nothing ? (
        <p className="text-muted-foreground">Nothing would change.</p>
      ) : (
        <>
          {preview.additions.length > 0 ? (
            <div>
              <h2 className="font-medium text-foreground">Will be added ({preview.additions.length})</h2>
              <ul className="list-disc pl-5 text-muted-foreground">
                {preview.additions.map((change) => (
                  <li key={`${change.subjectId}:${change.classLevelId}`}>
                    {change.subjectName} in {change.classLevelName}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
          {preview.endings.length > 0 ? (
            <div>
              <h2 className="font-medium text-foreground">Will be removed from this term ({preview.endings.length})</h2>
              <ul className="list-disc pl-5 text-muted-foreground">
                {preview.endings.map((change) => (
                  <li key={`${change.subjectId}:${change.classLevelId}`}>
                    {change.subjectName} in {change.classLevelName}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      )}
      <div className="flex gap-2">
        {nothing ? null : (
          <Button onClick={onApply} disabled={applying}>
            {applying ? 'Saving…' : 'Apply changes'}
          </Button>
        )}
        <Button variant="ghost" onClick={onCancel}>
          {nothing ? 'Close' : 'Cancel'}
        </Button>
      </div>
    </section>
  );
}
