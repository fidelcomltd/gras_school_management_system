import { Camera, Pencil, Trash2, UserRound } from 'lucide-react';
import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { FileButton } from '@/components/ui/file-button';
import { Spinner } from '@/components/ui/spinner';
import { usePupilPhotoUrl, useRemovePhoto, useUploadPhoto } from './files-api';
import { errorText } from './format';

/**
 * The pupil's photograph (spec 6.5.4): shown on the record, uploaded or replaced by anyone holding `pupil.photo.update`
 * from a camera (none yet) or pencil (replace) button on the photo itself, and removable (an audited action). Not
 * required: the school photographs a new intake weeks after admission.
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
  // The box is 96 px, so the 96 px rendition: the full 400 px one would spend a clerk's data for nothing.
  const photo = usePupilPhotoUrl(pupilId, photoUpdatedAtUtc, true);
  const upload = useUploadPhoto(pupilId);
  const remove = useRemovePhoto(pupilId);
  const [confirming, setConfirming] = useState(false);
  const busy = upload.isPending || remove.isPending;
  const hasPhoto = !!photoUpdatedAtUtc;

  return (
    <div className="flex items-start gap-4">
      <div className="relative size-24 shrink-0">
        <div className="flex size-full items-center justify-center overflow-hidden rounded-xl border border-border bg-muted text-xs text-muted-foreground">
          {upload.isPending || (hasPhoto && photo.isPending) ? (
            <output>
              <Spinner />
              <span className="sr-only">{upload.isPending ? 'Uploading the photograph…' : 'Loading the photograph…'}</span>
            </output>
          ) : photo.data ? (
            <img src={photo.data} alt={`Photograph of ${name}`} className="size-full object-cover" />
          ) : photo.isError ? (
            <button type="button" className="px-1 text-center text-destructive underline" onClick={() => void photo.refetch()}>
              Photograph unavailable. Retry
            </button>
          ) : (
            <>
              <UserRound className="size-10 text-muted-foreground/60" aria-hidden="true" />
              <span className="sr-only">No photograph</span>
            </>
          )}
        </div>
        {canEdit ? (
          <FileButton
            inputLabel={hasPhoto ? 'Replace photograph' : 'Upload photograph'}
            buttonLabel={hasPhoto ? 'Replace photograph' : 'Upload photograph'}
            accept="image/png,image/jpeg"
            onFile={(file) => upload.mutate(file)}
            disabled={busy}
            variant="primary"
            size="icon"
            className="absolute -right-2 -bottom-2 size-8 rounded-full shadow-md ring-2 ring-background"
          >
            {hasPhoto ? <Pencil aria-hidden="true" /> : <Camera aria-hidden="true" />}
          </FileButton>
        ) : null}
      </div>
      {canEdit ? (
        <div className="flex flex-col gap-1 pt-1">
          {!hasPhoto && !upload.isPending ? <p className="text-xs text-muted-foreground">No photograph yet. JPEG or PNG.</p> : null}
          {upload.isPending ? <output className="text-xs text-muted-foreground">Uploading…</output> : null}
          {hasPhoto && !confirming ? (
            <Button variant="ghost" size="sm" className="self-start text-muted-foreground" disabled={busy} onClick={() => setConfirming(true)}>
              <Trash2 aria-hidden="true" />
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
