import { useId, useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { usePupilPhotoUrl, useRemovePhoto, useUploadPhoto } from './files-api';
import { errorText } from './format';

/**
 * The pupil's photograph (spec 6.5.4): shown on the record, uploaded or replaced by anyone holding `pupil.photo.update`,
 * and removable (an audited action). Not required: the school photographs a new intake weeks after admission.
 */
export function PupilPhoto({
  pupilId,
  name,
  photoUpdatedAtUtc,
  canEdit,
}: {
  pupilId: string;
  name: string;
  photoUpdatedAtUtc: string | null | undefined;
  canEdit: boolean;
}) {
  const inputId = useId();
  const url = usePupilPhotoUrl(pupilId, photoUpdatedAtUtc);
  const upload = useUploadPhoto(pupilId);
  const remove = useRemovePhoto(pupilId);
  const [confirming, setConfirming] = useState(false);
  const busy = upload.isPending || remove.isPending;

  return (
    <div className="flex items-start gap-3">
      <div className="flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-md border border-border bg-muted text-xs text-muted-foreground">
        {url.data ? <img src={url.data} alt={`Photograph of ${name}`} className="size-full object-cover" /> : photoUpdatedAtUtc ? 'Loading…' : 'No photograph'}
      </div>
      {canEdit ? (
        <div className="flex flex-col gap-1">
          <label htmlFor={inputId} className="text-sm text-foreground">
            {photoUpdatedAtUtc ? 'Replace photograph' : 'Upload photograph'} (JPEG or PNG)
          </label>
          <input
            id={inputId}
            type="file"
            accept="image/png,image/jpeg"
            disabled={busy}
            className="text-sm"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) upload.mutate(file);
              event.target.value = '';
            }}
          />
          {upload.isPending ? <output className="text-sm text-muted-foreground">Uploading…</output> : null}
          {photoUpdatedAtUtc && !confirming ? (
            <Button variant="ghost" size="sm" className="self-start" disabled={busy} onClick={() => setConfirming(true)}>
              Remove photograph
            </Button>
          ) : null}
          {confirming ? (
            <div className="flex items-center gap-2 text-sm">
              <span className="text-foreground">Remove this photograph?</span>
              <Button variant="destructive" size="sm" disabled={busy} onClick={() => remove.mutate(undefined, { onSettled: () => setConfirming(false) })}>
                Remove
              </Button>
              <Button variant="ghost" size="sm" disabled={busy} onClick={() => setConfirming(false)}>
                Keep
              </Button>
            </div>
          ) : null}
          <FormError message={errorText(upload.error) ?? errorText(remove.error)} />
        </div>
      ) : null}
    </div>
  );
}
